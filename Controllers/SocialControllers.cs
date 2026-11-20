using Microsoft.AspNetCore.Mvc;
using NEXT.Services;
using NEXT.ViewModels;

namespace NEXT.Controllers;

public class ProfileController : AppController
{
    private readonly IProfileService _profiles;
    public ProfileController(IProfileService profiles) => _profiles = profiles;

    public async Task<IActionResult> Index(string username)
    {
        var vm = await _profiles.GetAsync(UserId, username);
        return vm is null ? NotFound() : View(vm);
    }
}

public class FriendsController : AppController
{
    private readonly IFriendService _friends;
    public FriendsController(IFriendService friends) => _friends = friends;

    public async Task<IActionResult> Index() => View(new FriendsPageViewModel
    {
        Friends = await _friends.GetFriendsAsync(UserId),
        Suggestions = await _friends.GetSuggestionsAsync(UserId)
    });

    public async Task<IActionResult> Requests() => View(new RequestsPageViewModel
    {
        Incoming = await _friends.GetIncomingAsync(UserId),
        Outgoing = await _friends.GetOutgoingAsync(UserId)
    });

    [HttpPost]
    public async Task<IActionResult> Send(string id, string? returnUrl)
    {
        var r = await _friends.SendRequestAsync(UserId, id);
        Toast(r.Succeeded ? "Done." : r.Error!, !r.Succeeded);
        return RedirectBack(returnUrl, nameof(Index), "Friends");
    }

    [HttpPost]
    public async Task<IActionResult> Accept(string id, string? returnUrl)
    {
        var r = await _friends.AcceptAsync(UserId, id);
        Toast(r.Succeeded ? "Friend request accepted." : r.Error!, !r.Succeeded);
        return RedirectBack(returnUrl, nameof(Requests), "Friends");
    }

    [HttpPost]
    public async Task<IActionResult> Reject(string id, string? returnUrl)
    {
        var r = await _friends.RejectAsync(UserId, id);
        Toast(r.Succeeded ? "Request declined." : r.Error!, !r.Succeeded);
        return RedirectBack(returnUrl, nameof(Requests), "Friends");
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(string id, string? returnUrl)
    {
        var r = await _friends.CancelAsync(UserId, id);
        Toast(r.Succeeded ? "Request cancelled." : r.Error!, !r.Succeeded);
        return RedirectBack(returnUrl, nameof(Requests), "Friends");
    }

    [HttpPost]
    public async Task<IActionResult> Remove(string id, string? returnUrl)
    {
        var r = await _friends.RemoveAsync(UserId, id);
        Toast(r.Succeeded ? "Friend removed." : r.Error!, !r.Succeeded);
        return RedirectBack(returnUrl, nameof(Index), "Friends");
    }

    [HttpPost]
    public async Task<IActionResult> Follow(string id, string? returnUrl)
    {
        var r = await _friends.ToggleFollowAsync(UserId, id);
        Toast(r.Succeeded ? (r.Data ? "You are now following this person." : "Unfollowed.") : r.Error!, !r.Succeeded);
        return RedirectBack(returnUrl, nameof(Index), "Friends");
    }
}

public class SearchController : AppController
{
    private readonly IFriendService _friends;
    public SearchController(IFriendService friends) => _friends = friends;

    public async Task<IActionResult> Index(string? q)
    {
        q = q?.Trim() ?? "";
        return View(new SearchViewModel
        {
            Query = q,
            Results = q.Length == 0 ? new() : await _friends.SearchAsync(UserId, q)
        });
    }
}

public class NotificationsController : AppController
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    public async Task<IActionResult> Index()
    {
        // Load first so unread items can be highlighted, then mark them read.
        var items = await _notifications.GetAsync(UserId);
        await _notifications.MarkAllReadAsync(UserId);
        return View(items);
    }
}

public class MessagesController : AppController
{
    private readonly IChatService _chat;
    public MessagesController(IChatService chat) => _chat = chat;

    public async Task<IActionResult> Index(string? with)
    {
        var vm = new MessagesPageViewModel();

        if (!string.IsNullOrWhiteSpace(with))
        {
            vm.Active = await _chat.GetConversationAsync(UserId, with);
            if (vm.Active is null) return NotFound();

            await _chat.MarkReadAsync(UserId, vm.Active.UserId);
            vm.Messages = await _chat.GetThreadAsync(UserId, vm.Active.UserId);
            vm.CanSend = await _chat.AreFriendsAsync(UserId, vm.Active.UserId);
        }

        vm.Conversations = await _chat.GetConversationsAsync(UserId);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Send(string recipientId, string? text, IFormFile? image)
    {
        var result = await _chat.SendAsync(UserId, recipientId, text, image);
        return result.Succeeded ? Ok(result.Data) : Fail(result);
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(string userId)
    {
        await _chat.MarkReadAsync(UserId, userId);
        return Ok();
    }
}
