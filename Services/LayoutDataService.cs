using Microsoft.EntityFrameworkCore;
using NEXT.Data;
using NEXT.Helpers;
using NEXT.Models;
using NEXT.ViewModels;

namespace NEXT.Services;

/// <summary>Per-request data the layout needs: current user, theme and navigation badges.</summary>
public class LayoutDataService
{
    private readonly IHttpContextAccessor _http;
    private readonly ApplicationDbContext _db;
    private LayoutData? _cached;

    public LayoutDataService(IHttpContextAccessor http, ApplicationDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task<LayoutData> GetAsync()
    {
        if (_cached is not null) return _cached;

        var principal = _http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return _cached = new LayoutData();

        var id = principal.GetUserId();
        var data = new LayoutData
        {
            User = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
        };
        if (data.User is not null)
        {
            data.UnreadNotifications = await _db.Notifications.CountAsync(n => n.UserId == id && !n.IsRead);
            data.UnreadMessages = await _db.Messages.CountAsync(m => m.RecipientId == id && !m.IsRead);
            data.PendingRequests = await _db.Friendships
                .CountAsync(f => f.AddresseeId == id && f.Status == FriendshipStatus.Pending);
        }
        return _cached = data;
    }
}
