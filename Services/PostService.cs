using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NEXT.Data;
using NEXT.Helpers;
using NEXT.Models;
using NEXT.ViewModels;

namespace NEXT.Services;

public interface IPostService
{
    const int PageSize = 10;
    Task<(List<PostViewModel> Posts, bool HasMore)> GetFeedAsync(string me, FeedScope scope, string? profileUserId, int page);
    Task<PostViewModel?> GetAsync(string me, int postId);
    Task<ServiceResult<int>> CreateAsync(string me, string? content, IFormFile? image);
    Task<ServiceResult> EditAsync(ClaimsPrincipal user, int postId, string? content);
    Task<ServiceResult> DeleteAsync(ClaimsPrincipal user, int postId);
    Task<ServiceResult<(bool Liked, int Count)>> ToggleLikeAsync(string me, int postId);
    Task<ServiceResult<int>> ShareAsync(string me, int postId, string? content);
    Task<List<CommentViewModel>> GetCommentsAsync(string me, int postId);
    Task<ServiceResult<CommentViewModel>> AddCommentAsync(string me, int postId, string? content);
    Task<ServiceResult<int>> DeleteCommentAsync(ClaimsPrincipal user, int commentId);
}

public class PostService : IPostService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly INotificationService _notifications;
    private readonly IAuthorizationService _auth;

    public PostService(ApplicationDbContext db, IFileStorageService files,
        INotificationService notifications, IAuthorizationService auth)
    {
        _db = db;
        _files = files;
        _notifications = notifications;
        _auth = auth;
    }

    private static IQueryable<PostViewModel> Project(IQueryable<Post> posts, string me) =>
        posts.Select(p => new PostViewModel
        {
            Id = p.Id,
            AuthorId = p.AuthorId,
            AuthorName = p.Author.FullName,
            AuthorUserName = p.Author.UserName!,
            AuthorAvatar = p.Author.ProfilePicturePath,
            Content = p.Content,
            ImagePath = p.ImagePath,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            LikeCount = p.Likes.Count,
            CommentCount = p.Comments.Count,
            ShareCount = p.Shares.Count,
            LikedByMe = p.Likes.Any(l => l.UserId == me),
            IsOwner = p.AuthorId == me,
            IsShare = p.SharedPostId != null,
            Shared = p.SharedPost == null ? null : new SharedPostViewModel
            {
                Id = p.SharedPost.Id,
                AuthorName = p.SharedPost.Author.FullName,
                AuthorUserName = p.SharedPost.Author.UserName!,
                AuthorAvatar = p.SharedPost.Author.ProfilePicturePath,
                Content = p.SharedPost.Content,
                ImagePath = p.SharedPost.ImagePath,
                CreatedAt = p.SharedPost.CreatedAt
            }
        });

    public async Task<(List<PostViewModel> Posts, bool HasMore)> GetFeedAsync(
        string me, FeedScope scope, string? profileUserId, int page)
    {
        page = Math.Max(1, page);
        var query = _db.Posts.AsNoTracking().AsQueryable();

        if (profileUserId is not null)
        {
            query = query.Where(p => p.AuthorId == profileUserId);
        }
        else if (scope == FeedScope.Network)
        {
            var friendIds = _db.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == me || f.AddresseeId == me))
                .Select(f => f.RequesterId == me ? f.AddresseeId : f.RequesterId);
            var followingIds = _db.Follows.Where(f => f.FollowerId == me).Select(f => f.FollowingId);
            query = query.Where(p => p.AuthorId == me || friendIds.Contains(p.AuthorId) || followingIds.Contains(p.AuthorId));
        }

        var paged = query
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * IPostService.PageSize)
            .Take(IPostService.PageSize + 1);

        var items = await Project(paged, me).ToListAsync();
        var hasMore = items.Count > IPostService.PageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return (items, hasMore);
    }

    public Task<PostViewModel?> GetAsync(string me, int postId) =>
        Project(_db.Posts.AsNoTracking().Where(p => p.Id == postId), me).FirstOrDefaultAsync();

    public async Task<ServiceResult<int>> CreateAsync(string me, string? content, IFormFile? image)
    {
        content = content?.Trim() ?? "";
        if (content.Length > 2000) return ServiceResult<int>.Fail("Posts can be up to 2000 characters.");
        if (content.Length == 0 && image is null) return ServiceResult<int>.Fail("Write something or add a photo.");

        string? imagePath = null;
        if (image is not null)
        {
            var saved = await _files.SaveImageAsync(image, "posts");
            if (!saved.Succeeded) return ServiceResult<int>.Fail(saved.Error!);
            imagePath = saved.Data;
        }

        var post = new Post { AuthorId = me, Content = content, ImagePath = imagePath };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return ServiceResult<int>.Ok(post.Id);
    }

    public async Task<ServiceResult> EditAsync(ClaimsPrincipal user, int postId, string? content)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post is null) return ServiceResult.Fail("This post no longer exists.");
        if (!(await _auth.AuthorizeAsync(user, post, OwnershipOperations.Modify)).Succeeded)
            return ServiceResult.Forbidden();

        content = content?.Trim() ?? "";
        if (content.Length > 2000) return ServiceResult.Fail("Posts can be up to 2000 characters.");
        if (content.Length == 0 && post.ImagePath is null && post.SharedPostId is null)
            return ServiceResult.Fail("A post can't be empty.");

        if (content != post.Content)
        {
            post.Content = content;
            post.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(ClaimsPrincipal user, int postId)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post is null) return ServiceResult.Fail("This post no longer exists.");
        if (!(await _auth.AuthorizeAsync(user, post, OwnershipOperations.Modify)).Succeeded)
            return ServiceResult.Forbidden();

        // Shares of this post stay, but lose their link; likes/comments cascade in the database.
        await _db.Posts.Where(p => p.SharedPostId == postId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.SharedPostId, p => (int?)null));
        await _db.Notifications.Where(n => n.PostId == postId).ExecuteDeleteAsync();

        var imagePath = post.ImagePath;
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        _files.Delete(imagePath);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<(bool Liked, int Count)>> ToggleLikeAsync(string me, int postId)
    {
        var authorId = await _db.Posts.Where(p => p.Id == postId).Select(p => p.AuthorId).FirstOrDefaultAsync();
        if (authorId is null) return ServiceResult<(bool, int)>.Fail("This post no longer exists.");

        var like = await _db.PostLikes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == me);
        bool liked;
        if (like is null)
        {
            _db.PostLikes.Add(new PostLike { PostId = postId, UserId = me });
            liked = true;
        }
        else
        {
            _db.PostLikes.Remove(like);
            liked = false;
        }

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* double-click race hit the unique index; the count below is still right */ }

        var count = await _db.PostLikes.CountAsync(l => l.PostId == postId);
        if (liked) await _notifications.NotifyAsync(authorId, me, NotificationType.PostLike, $"/Posts/Details/{postId}", postId);
        return ServiceResult<(bool, int)>.Ok((liked, count));
    }

    public async Task<ServiceResult<int>> ShareAsync(string me, int postId, string? content)
    {
        var original = await _db.Posts.AsNoTracking()
            .Where(p => p.Id == postId)
            .Select(p => new { p.Id, p.AuthorId, p.SharedPostId })
            .FirstOrDefaultAsync();
        if (original is null) return ServiceResult<int>.Fail("This post no longer exists.");

        // Sharing a share points at the original post, so shares never nest.
        var rootId = original.SharedPostId ?? original.Id;
        var rootAuthorId = original.SharedPostId is null
            ? original.AuthorId
            : await _db.Posts.Where(p => p.Id == rootId).Select(p => p.AuthorId).FirstAsync();

        if (rootAuthorId == me) return ServiceResult<int>.Fail("You can't share your own post.");

        content = content?.Trim() ?? "";
        if (content.Length > 2000) return ServiceResult<int>.Fail("Posts can be up to 2000 characters.");

        var share = new Post { AuthorId = me, Content = content, SharedPostId = rootId };
        _db.Posts.Add(share);
        await _db.SaveChangesAsync();
        await _notifications.NotifyAsync(rootAuthorId, me, NotificationType.PostShare, $"/Posts/Details/{share.Id}", rootId);
        return ServiceResult<int>.Ok(share.Id);
    }

    public Task<List<CommentViewModel>> GetCommentsAsync(string me, int postId) =>
        _db.Comments.AsNoTracking()
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Take(200)
            .Select(c => new CommentViewModel
            {
                Id = c.Id, PostId = c.PostId, AuthorName = c.Author.FullName, AuthorUserName = c.Author.UserName!,
                AuthorAvatar = c.Author.ProfilePicturePath, Content = c.Content, CreatedAt = c.CreatedAt,
                IsOwner = c.AuthorId == me
            })
            .ToListAsync();

    public async Task<ServiceResult<CommentViewModel>> AddCommentAsync(string me, int postId, string? content)
    {
        content = content?.Trim() ?? "";
        if (content.Length == 0) return ServiceResult<CommentViewModel>.Fail("Write a comment first.");
        if (content.Length > 1000) return ServiceResult<CommentViewModel>.Fail("Comments can be up to 1000 characters.");

        var authorId = await _db.Posts.Where(p => p.Id == postId).Select(p => p.AuthorId).FirstOrDefaultAsync();
        if (authorId is null) return ServiceResult<CommentViewModel>.Fail("This post no longer exists.");

        var comment = new Comment { PostId = postId, AuthorId = me, Content = content };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        await _notifications.NotifyAsync(authorId, me, NotificationType.PostComment, $"/Posts/Details/{postId}", postId);

        var vm = await _db.Comments.AsNoTracking().Where(c => c.Id == comment.Id)
            .Select(c => new CommentViewModel
            {
                Id = c.Id, PostId = c.PostId, AuthorName = c.Author.FullName, AuthorUserName = c.Author.UserName!,
                AuthorAvatar = c.Author.ProfilePicturePath, Content = c.Content, CreatedAt = c.CreatedAt, IsOwner = true
            }).FirstAsync();
        return ServiceResult<CommentViewModel>.Ok(vm);
    }

    public async Task<ServiceResult<int>> DeleteCommentAsync(ClaimsPrincipal user, int commentId)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null) return ServiceResult<int>.Fail("This comment no longer exists.");
        if (!(await _auth.AuthorizeAsync(user, comment, OwnershipOperations.Modify)).Succeeded)
            return ServiceResult<int>.Forbidden();

        var postId = comment.PostId;
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        var remaining = await _db.Comments.CountAsync(c => c.PostId == postId);
        return ServiceResult<int>.Ok(remaining);
    }
}
