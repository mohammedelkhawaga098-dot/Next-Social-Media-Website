using Microsoft.EntityFrameworkCore;
using NEXT.Data;
using NEXT.Helpers;
using NEXT.Models;
using NEXT.ViewModels;

namespace NEXT.Services;

public interface IFriendService
{
    Task<RelationState> GetStateAsync(string me, string other);
    Task<ServiceResult> SendRequestAsync(string me, string targetId);
    Task<ServiceResult> AcceptAsync(string me, string requesterId);
    Task<ServiceResult> RejectAsync(string me, string requesterId);
    Task<ServiceResult> CancelAsync(string me, string targetId);
    Task<ServiceResult> RemoveAsync(string me, string friendId);
    Task<ServiceResult<bool>> ToggleFollowAsync(string me, string targetId);
    Task<List<UserCardViewModel>> GetFriendsAsync(string userId, int? take = null);
    Task<List<UserCardViewModel>> GetIncomingAsync(string me);
    Task<List<UserCardViewModel>> GetOutgoingAsync(string me);
    Task<List<UserCardViewModel>> GetSuggestionsAsync(string me, int take = 12);
    Task<List<UserCardViewModel>> SearchAsync(string me, string query, int take = 40);
}

public class FriendService : IFriendService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;

    public FriendService(ApplicationDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    private Task<Friendship?> FindAsync(string a, string b) =>
        _db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == a && f.AddresseeId == b) || (f.RequesterId == b && f.AddresseeId == a));

    public async Task<RelationState> GetStateAsync(string me, string other)
    {
        if (me == other) return RelationState.Self;
        var f = await _db.Friendships.AsNoTracking().FirstOrDefaultAsync(x =>
            (x.RequesterId == me && x.AddresseeId == other) || (x.RequesterId == other && x.AddresseeId == me));
        return ToState(f, me);
    }

    private static RelationState ToState(Friendship? f, string me)
    {
        if (f is null) return RelationState.None;
        if (f.Status == FriendshipStatus.Accepted) return RelationState.Friends;
        return f.RequesterId == me ? RelationState.RequestSent : RelationState.RequestReceived;
    }

    public async Task<ServiceResult> SendRequestAsync(string me, string targetId)
    {
        if (me == targetId) return ServiceResult.Fail("You can't send a friend request to yourself.");
        var target = await _db.Users.AsNoTracking().Where(u => u.Id == targetId)
            .Select(u => new { u.Id }).FirstOrDefaultAsync();
        if (target is null) return ServiceResult.Fail("That user doesn't exist.");

        var existing = await FindAsync(me, targetId);
        if (existing is not null)
        {
            if (existing.Status == FriendshipStatus.Accepted) return ServiceResult.Fail("You are already friends.");
            if (existing.RequesterId == me) return ServiceResult.Fail("You already sent a request to this person.");
            return await AcceptAsync(me, targetId); // they already asked us: sending one back just accepts
        }

        _db.Friendships.Add(new Friendship { RequesterId = me, AddresseeId = targetId });
        await _db.SaveChangesAsync();
        await _notifications.NotifyAsync(targetId, me, NotificationType.FriendRequest, "/Friends/Requests");
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AcceptAsync(string me, string requesterId)
    {
        var f = await _db.Friendships.FirstOrDefaultAsync(x =>
            x.RequesterId == requesterId && x.AddresseeId == me && x.Status == FriendshipStatus.Pending);
        if (f is null) return ServiceResult.Fail("This friend request no longer exists.");

        f.Status = FriendshipStatus.Accepted;
        f.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var myUserName = await _db.Users.Where(u => u.Id == me).Select(u => u.UserName).FirstOrDefaultAsync();
        await _notifications.NotifyAsync(requesterId, me, NotificationType.FriendAccepted, $"/u/{myUserName}");
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RejectAsync(string me, string requesterId)
    {
        var f = await _db.Friendships.FirstOrDefaultAsync(x =>
            x.RequesterId == requesterId && x.AddresseeId == me && x.Status == FriendshipStatus.Pending);
        if (f is null) return ServiceResult.Fail("This friend request no longer exists.");
        _db.Friendships.Remove(f);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelAsync(string me, string targetId)
    {
        var f = await _db.Friendships.FirstOrDefaultAsync(x =>
            x.RequesterId == me && x.AddresseeId == targetId && x.Status == FriendshipStatus.Pending);
        if (f is null) return ServiceResult.Fail("There is no pending request to cancel.");
        _db.Friendships.Remove(f);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveAsync(string me, string friendId)
    {
        var f = await FindAsync(me, friendId);
        if (f is null || f.Status != FriendshipStatus.Accepted) return ServiceResult.Fail("You are not friends.");
        _db.Friendships.Remove(f);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<bool>> ToggleFollowAsync(string me, string targetId)
    {
        if (me == targetId) return ServiceResult<bool>.Fail("You can't follow yourself.");
        if (!await _db.Users.AnyAsync(u => u.Id == targetId)) return ServiceResult<bool>.Fail("That user doesn't exist.");

        var follow = await _db.Follows.FirstOrDefaultAsync(f => f.FollowerId == me && f.FollowingId == targetId);
        if (follow is null)
        {
            _db.Follows.Add(new Follow { FollowerId = me, FollowingId = targetId });
            await _db.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }
        _db.Follows.Remove(follow);
        await _db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(false);
    }

    // ---------- Lists ----------

    private async Task<List<UserCardViewModel>> ToCardsAsync(string me, IQueryable<ApplicationUser> users)
    {
        var list = await users.AsNoTracking().Select(u => new UserCardViewModel
        {
            Id = u.Id, FullName = u.FullName, UserName = u.UserName!, Avatar = u.ProfilePicturePath, Bio = u.Bio
        }).ToListAsync();

        if (list.Count == 0) return list;

        var ids = list.Select(c => c.Id).ToList();
        var relations = await _db.Friendships.AsNoTracking()
            .Where(f => (f.RequesterId == me && ids.Contains(f.AddresseeId)) || (f.AddresseeId == me && ids.Contains(f.RequesterId)))
            .ToListAsync();

        foreach (var c in list)
        {
            if (c.Id == me) { c.State = RelationState.Self; continue; }
            var f = relations.FirstOrDefault(x => x.RequesterId == c.Id || x.AddresseeId == c.Id);
            c.State = ToState(f, me);
        }
        return list;
    }

    public async Task<List<UserCardViewModel>> GetFriendsAsync(string userId, int? take = null)
    {
        var friendIds = _db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId);

        var users = _db.Users.Where(u => friendIds.Contains(u.Id)).OrderBy(u => u.FullName).AsQueryable();
        if (take.HasValue) users = users.Take(take.Value);
        return await ToCardsAsync(userId, users);
    }

    public Task<List<UserCardViewModel>> GetIncomingAsync(string me)
    {
        var ids = _db.Friendships
            .Where(f => f.AddresseeId == me && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedAt).Select(f => f.RequesterId);
        return ToCardsAsync(me, _db.Users.Where(u => ids.Contains(u.Id)));
    }

    public Task<List<UserCardViewModel>> GetOutgoingAsync(string me)
    {
        var ids = _db.Friendships
            .Where(f => f.RequesterId == me && f.Status == FriendshipStatus.Pending)
            .Select(f => f.AddresseeId);
        return ToCardsAsync(me, _db.Users.Where(u => ids.Contains(u.Id)));
    }

    public Task<List<UserCardViewModel>> GetSuggestionsAsync(string me, int take = 12)
    {
        var related = _db.Friendships
            .Where(f => f.RequesterId == me || f.AddresseeId == me)
            .Select(f => f.RequesterId == me ? f.AddresseeId : f.RequesterId);

        var users = _db.Users
            .Where(u => u.Id != me && !related.Contains(u.Id))
            .OrderByDescending(u => u.CreatedAt)
            .Take(take);
        return ToCardsAsync(me, users);
    }

    public Task<List<UserCardViewModel>> SearchAsync(string me, string query, int take = 40)
    {
        query = query.Trim();
        if (query.Length > 50) query = query[..50];
        var users = _db.Users
            .Where(u => u.Id != me && (u.UserName!.Contains(query) || u.FullName.Contains(query)))
            .OrderBy(u => u.FullName)
            .Take(take);
        return ToCardsAsync(me, users);
    }
}
