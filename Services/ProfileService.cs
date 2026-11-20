using Microsoft.EntityFrameworkCore;
using NEXT.Data;
using NEXT.Models;
using NEXT.ViewModels;

namespace NEXT.Services;

public interface IProfileService
{
    Task<ProfileViewModel?> GetAsync(string me, string userName);
}

public class ProfileService : IProfileService
{
    private readonly ApplicationDbContext _db;
    private readonly IFriendService _friends;

    public ProfileService(ApplicationDbContext db, IFriendService friends)
    {
        _db = db;
        _friends = friends;
    }

    public async Task<ProfileViewModel?> GetAsync(string me, string userName)
    {
        var normalized = userName.ToUpperInvariant();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUserName == normalized);
        if (user is null) return null;

        var vm = new ProfileViewModel
        {
            Id = user.Id, FullName = user.FullName, UserName = user.UserName!, Bio = user.Bio,
            Avatar = user.ProfilePicturePath, Cover = user.CoverPhotoPath, JoinedAt = user.CreatedAt,
            PostCount = await _db.Posts.CountAsync(p => p.AuthorId == user.Id),
            FriendCount = await _db.Friendships.CountAsync(f =>
                f.Status == FriendshipStatus.Accepted && (f.RequesterId == user.Id || f.AddresseeId == user.Id)),
            FollowerCount = await _db.Follows.CountAsync(f => f.FollowingId == user.Id),
            FollowingCount = await _db.Follows.CountAsync(f => f.FollowerId == user.Id),
            State = await _friends.GetStateAsync(me, user.Id),
            FriendPreview = await _friends.GetFriendsAsync(user.Id, 6)
        };
        vm.IsFollowing = me != user.Id && await _db.Follows.AnyAsync(f => f.FollowerId == me && f.FollowingId == user.Id);
        return vm;
    }
}
