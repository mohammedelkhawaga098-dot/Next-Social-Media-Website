using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NEXT.Data;
using NEXT.Helpers;
using NEXT.Hubs;
using NEXT.Models;
using NEXT.ViewModels;

namespace NEXT.Services;

public interface IChatService
{
    Task<List<ConversationViewModel>> GetConversationsAsync(string me);
    Task<ConversationViewModel?> GetConversationAsync(string me, string userName);
    Task<bool> AreFriendsAsync(string a, string b);
    Task<List<MessageViewModel>> GetThreadAsync(string me, string otherId, int take = 100);
    Task<ServiceResult<MessageViewModel>> SendAsync(string me, string recipientId, string? text, IFormFile? image);
    Task MarkReadAsync(string me, string otherId);
}

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly INotificationService _notifications;
    private readonly IHubContext<NextHub> _hub;
    private readonly ILogger<ChatService> _log;

    public ChatService(ApplicationDbContext db, IFileStorageService files, INotificationService notifications,
        IHubContext<NextHub> hub, ILogger<ChatService> log)
    {
        _db = db;
        _files = files;
        _notifications = notifications;
        _hub = hub;
        _log = log;
    }

    public Task<bool> AreFriendsAsync(string a, string b) =>
        _db.Friendships.AnyAsync(f => f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == a && f.AddresseeId == b) || (f.RequesterId == b && f.AddresseeId == a)));

    public async Task<List<ConversationViewModel>> GetConversationsAsync(string me)
    {
        var stats = await _db.Messages
            .Where(m => m.SenderId == me || m.RecipientId == me)
            .GroupBy(m => m.SenderId == me ? m.RecipientId : m.SenderId)
            .Select(g => new
            {
                OtherId = g.Key,
                LastId = g.Max(m => m.Id),
                Unread = g.Count(m => m.RecipientId == me && !m.IsRead)
            })
            .ToListAsync();

        var lastIds = stats.Select(s => s.LastId).ToList();
        var otherIds = stats.Select(s => s.OtherId).ToList();

        var lastMessages = await _db.Messages.AsNoTracking()
            .Where(m => lastIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        // Friends without any messages yet are listed too, so a chat can be started from here.
        var friendIds = await _db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == me || f.AddresseeId == me))
            .Select(f => f.RequesterId == me ? f.AddresseeId : f.RequesterId)
            .ToListAsync();
        var allIds = otherIds.Union(friendIds).ToList();

        var users = await _db.Users.AsNoTracking()
            .Where(u => allIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var result = new List<ConversationViewModel>();
        foreach (var id in allIds)
        {
            if (!users.TryGetValue(id, out var u)) continue;
            var s = stats.FirstOrDefault(x => x.OtherId == id);
            lastMessages.TryGetValue(s?.LastId ?? 0, out var last);

            result.Add(new ConversationViewModel
            {
                UserId = u.Id, FullName = u.FullName, UserName = u.UserName!, Avatar = u.ProfilePicturePath,
                Unread = s?.Unread ?? 0,
                LastMessage = last is null ? null : (last.Content.Length > 0 ? last.Content : "Photo"),
                LastMessageAt = last?.SentAt,
                LastMessageMine = last?.SenderId == me
            });
        }

        return result
            .OrderByDescending(c => c.LastMessageAt.HasValue)
            .ThenByDescending(c => c.LastMessageAt)
            .ThenBy(c => c.FullName)
            .ToList();
    }

    public Task<ConversationViewModel?> GetConversationAsync(string me, string userName) =>
        _db.Users.AsNoTracking()
            .Where(u => u.NormalizedUserName == userName.ToUpper() && u.Id != me)
            .Select(u => new ConversationViewModel
            {
                UserId = u.Id, FullName = u.FullName, UserName = u.UserName!, Avatar = u.ProfilePicturePath
            })
            .FirstOrDefaultAsync();

    public async Task<List<MessageViewModel>> GetThreadAsync(string me, string otherId, int take = 100)
    {
        var messages = await _db.Messages.AsNoTracking()
            .Where(m => (m.SenderId == me && m.RecipientId == otherId) || (m.SenderId == otherId && m.RecipientId == me))
            .OrderByDescending(m => m.Id)
            .Take(take)
            .Select(m => new MessageViewModel
            {
                Id = m.Id, SenderId = m.SenderId, Content = m.Content, ImagePath = m.ImagePath,
                SentAt = m.SentAt, IsMine = m.SenderId == me, IsRead = m.IsRead
            })
            .ToListAsync();
        messages.Reverse();
        return messages;
    }

    public async Task<ServiceResult<MessageViewModel>> SendAsync(string me, string recipientId, string? text, IFormFile? image)
    {
        if (me == recipientId) return ServiceResult<MessageViewModel>.Fail("You can't message yourself.");

        var recipient = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == recipientId);
        if (!recipient) return ServiceResult<MessageViewModel>.Fail("That user doesn't exist.");
        if (!await AreFriendsAsync(me, recipientId))
            return ServiceResult<MessageViewModel>.Fail("You can only message your friends.");

        text = text?.Trim() ?? "";
        if (text.Length > 2000) return ServiceResult<MessageViewModel>.Fail("Messages can be up to 2000 characters.");
        if (text.Length == 0 && image is null) return ServiceResult<MessageViewModel>.Fail("Write a message or attach a photo.");

        string? imagePath = null;
        if (image is not null)
        {
            var saved = await _files.SaveImageAsync(image, "messages");
            if (!saved.Succeeded) return ServiceResult<MessageViewModel>.Fail(saved.Error!);
            imagePath = saved.Data;
        }

        var message = new Message { SenderId = me, RecipientId = recipientId, Content = text, ImagePath = imagePath };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        var sender = await _db.Users.AsNoTracking().Where(u => u.Id == me)
            .Select(u => new { u.FullName, u.UserName, u.ProfilePicturePath }).FirstAsync();

        try
        {
            await _hub.Clients.User(recipientId).SendAsync("ReceiveMessage", new
            {
                id = message.Id,
                senderId = me,
                senderName = sender.FullName,
                senderUserName = sender.UserName,
                senderAvatar = sender.ProfilePicturePath,
                content = message.Content,
                imagePath = message.ImagePath,
                sentAt = DateTime.SpecifyKind(message.SentAt, DateTimeKind.Utc)
            });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not push message {Id}", message.Id);
        }

        await _notifications.NotifyAsync(recipientId, me, NotificationType.Message, $"/Messages?with={sender.UserName}");

        return ServiceResult<MessageViewModel>.Ok(new MessageViewModel
        {
            Id = message.Id, SenderId = me, Content = message.Content, ImagePath = message.ImagePath,
            SentAt = message.SentAt, IsMine = true
        });
    }

    public async Task MarkReadAsync(string me, string otherId)
    {
        await _db.Messages
            .Where(m => m.SenderId == otherId && m.RecipientId == me && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

        await _db.Notifications
            .Where(n => n.UserId == me && n.ActorId == otherId && n.Type == NotificationType.Message && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
