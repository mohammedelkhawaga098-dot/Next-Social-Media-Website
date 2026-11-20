using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NEXT.Data;
using NEXT.Hubs;
using NEXT.Models;
using NEXT.ViewModels;

namespace NEXT.Services;

public interface INotificationService
{
    Task NotifyAsync(string recipientId, string actorId, NotificationType type, string url, int? postId = null);
    Task<List<NotificationViewModel>> GetAsync(string userId, int take = 50);
    Task MarkAllReadAsync(string userId);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NextHub> _hub;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(ApplicationDbContext db, IHubContext<NextHub> hub, ILogger<NotificationService> log)
    {
        _db = db;
        _hub = hub;
        _log = log;
    }

    public Task<List<NotificationViewModel>> GetAsync(string userId, int take = 50) =>
        _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(take)
            .Select(n => new NotificationViewModel
            {
                Id = n.Id, Text = n.Text, Url = n.Url, Type = n.Type, IsRead = n.IsRead, CreatedAt = n.CreatedAt,
                ActorName = n.Actor.FullName, ActorAvatar = n.Actor.ProfilePicturePath
            })
            .ToListAsync();

    public Task MarkAllReadAsync(string userId) =>
        _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    public async Task NotifyAsync(string recipientId, string actorId, NotificationType type, string url, int? postId = null)
    {
        if (recipientId == actorId) return; // never notify people about their own actions

        var actor = await _db.Users.AsNoTracking()
            .Where(u => u.Id == actorId)
            .Select(u => new { u.FullName, u.ProfilePicturePath })
            .FirstOrDefaultAsync();
        if (actor is null) return;

        var text = type switch
        {
            NotificationType.FriendRequest => $"{actor.FullName} sent you a friend request",
            NotificationType.FriendAccepted => $"{actor.FullName} accepted your friend request",
            NotificationType.PostLike => $"{actor.FullName} liked your post",
            NotificationType.PostComment => $"{actor.FullName} commented on your post",
            NotificationType.PostShare => $"{actor.FullName} shared your post",
            NotificationType.Message => $"{actor.FullName} sent you a message",
            _ => $"{actor.FullName} interacted with you"
        };

        // Avoid spam: one unread notification per (actor, type, post); messages collapse per sender.
        var existingQuery = _db.Notifications.Where(n =>
            n.UserId == recipientId && n.ActorId == actorId && n.Type == type && n.PostId == postId);
        if (type != NotificationType.PostLike) existingQuery = existingQuery.Where(n => !n.IsRead);
        var existing = await existingQuery.FirstOrDefaultAsync();

        Notification notification;
        if (existing is not null)
        {
            if (type == NotificationType.PostLike) return; // already told them about this like
            existing.CreatedAt = DateTime.UtcNow;
            existing.Text = text;
            existing.Url = url;
            notification = existing;
        }
        else
        {
            notification = new Notification
            {
                UserId = recipientId, ActorId = actorId, Type = type, PostId = postId, Text = text, Url = url
            };
            _db.Notifications.Add(notification);
        }
        await _db.SaveChangesAsync();

        try
        {
            var unread = await _db.Notifications.CountAsync(n => n.UserId == recipientId && !n.IsRead);
            await _hub.Clients.User(recipientId).SendAsync("ReceiveNotification", new
            {
                id = notification.Id,
                text = notification.Text,
                url = notification.Url,
                type = type.ToString(),
                actorAvatar = actor.ProfilePicturePath,
                actorName = actor.FullName,
                unreadCount = unread
            });
        }
        catch (Exception ex)
        {
            // The notification is saved; a failed live push must not break the user's action.
            _log.LogWarning(ex, "Could not push notification {Id} to {User}", notification.Id, recipientId);
        }
    }
}
