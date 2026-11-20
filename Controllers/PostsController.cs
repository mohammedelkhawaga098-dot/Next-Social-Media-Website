using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NEXT.Models;
using NEXT.Services;
using NEXT.ViewModels;

namespace NEXT.Controllers;

public class PostsController : AppController
{
    private readonly IPostService _posts;
    private readonly UserManager<ApplicationUser> _users;

    public PostsController(IPostService posts, UserManager<ApplicationUser> users)
    {
        _posts = posts;
        _users = users;
    }

    /// <summary>Renders one page of posts as HTML (home feed or a profile's posts).</summary>
    [HttpGet]
    public async Task<IActionResult> Feed(FeedScope scope = FeedScope.Network, string? userName = null, int page = 1)
    {
        string? profileUserId = null;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var profileUser = await _users.FindByNameAsync(userName);
            if (profileUser is null) return NotFound();
            profileUserId = profileUser.Id;
        }

        var (posts, hasMore) = await _posts.GetFeedAsync(UserId, scope, profileUserId, page);
        return PartialView("_PostList", new PostListViewModel
        {
            Posts = posts, HasMore = hasMore, Page = Math.Max(1, page), Scope = scope,
            UserName = userName, IsOwnProfile = profileUserId == UserId
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var post = await _posts.GetAsync(UserId, id);
        return post is null ? NotFound() : View(post);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string? content, IFormFile? image)
    {
        var result = await _posts.CreateAsync(UserId, content, image);
        if (!result.Succeeded) return Fail(result);
        return PartialView("_PostCard", await _posts.GetAsync(UserId, result.Data));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string? content)
    {
        var result = await _posts.EditAsync(User, id, content);
        if (!result.Succeeded) return Fail(result);
        return PartialView("_PostCard", await _posts.GetAsync(UserId, id));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _posts.DeleteAsync(User, id);
        return result.Succeeded ? Ok(new { ok = true }) : Fail(result);
    }

    [HttpPost]
    public async Task<IActionResult> Like(int id)
    {
        var result = await _posts.ToggleLikeAsync(UserId, id);
        if (!result.Succeeded) return Fail(result);
        return Ok(new { liked = result.Data.Liked, count = result.Data.Count });
    }

    [HttpPost]
    public async Task<IActionResult> Share(int id, string? content)
    {
        var result = await _posts.ShareAsync(UserId, id, content);
        return result.Succeeded ? Ok(new { ok = true, id = result.Data }) : Fail(result);
    }

    [HttpGet]
    public async Task<IActionResult> Comments(int id) =>
        PartialView("_Comments", new CommentsVm { PostId = id, Comments = await _posts.GetCommentsAsync(UserId, id) });

    [HttpPost]
    public async Task<IActionResult> AddComment(int id, string? content)
    {
        var result = await _posts.AddCommentAsync(UserId, id, content);
        return result.Succeeded ? PartialView("_Comment", result.Data) : Fail(result);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var result = await _posts.DeleteCommentAsync(User, id);
        return result.Succeeded ? Ok(new { ok = true, count = result.Data }) : Fail(result);
    }
}
