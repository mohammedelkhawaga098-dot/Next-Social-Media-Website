using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NEXT.Data;
using NEXT.Models;

namespace NEXT.Hubs;

public interface IPresenceTracker
{
    bool IsOnline(string userId);
    /// <returns>true if this was the user's first open connection.</returns>
    bool Connected(string userId, string connectionId);
    /// <returns>true if that was the user's last open connection.</returns>
    bool Disconnected(string userId, string connectionId);
}

public class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

    public bool IsOnline(string userId) => _connections.ContainsKey(userId);

    public bool Connected(string userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (set)
        {
            set.Add(connectionId);
            return set.Count == 1;
        }
    }

    public bool Disconnected(string userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set)) return false;
        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count > 0) return false;
        }
        _connections.TryRemove(userId, out _);
        return true;
    }
}

/// <summary>
/// One hub for the whole app. Messages and notifications are saved by services (so they are validated
/// and persisted in SQL Server) and pushed to the recipient through IHubContext. The hub itself tracks
/// presence and tells a user's friends when they go online or offline.
/// </summary>
[Authorize]
public class NextHub : Hub
{
    private readonly IPresenceTracker _presence;
    private readonly ApplicationDbContext _db;

    public NextHub(IPresenceTracker presence, ApplicationDbContext db)
    {
        _presence = presence;
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier!;
        if (_presence.Connected(userId, Context.ConnectionId))
            await BroadcastPresenceAsync(userId, true);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier!;
        if (_presence.Disconnected(userId, Context.ConnectionId))
        {
            await _db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, DateTime.UtcNow));
            await BroadcastPresenceAsync(userId, false);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastPresenceAsync(string userId, bool online)
    {
        var friendIds = await _db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        if (friendIds.Count > 0)
            await Clients.Users(friendIds).SendAsync("PresenceChanged", userId, online);
    }
}
