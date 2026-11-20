using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace NEXT.Helpers;

public static class TimeHelper
{
    /// <summary>ISO-8601 UTC string; the browser turns it into the viewer's local time.</summary>
    public static string Iso(this DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}

public static class ClaimsExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The current user has no id claim.");
}

public record ServiceResult(bool Succeeded, string? Error = null, bool IsForbidden = false)
{
    public static ServiceResult Ok() => new(true);
    public static ServiceResult Fail(string error) => new(false, error);
    public static ServiceResult Forbidden() => new(false, "You are not allowed to change this item.", true);
}

public record ServiceResult<T>(bool Succeeded, T? Data, string? Error = null, bool IsForbidden = false)
{
    public static ServiceResult<T> Ok(T data) => new(true, data);
    public static ServiceResult<T> Fail(string error) => new(false, default, error);
    public static ServiceResult<T> Forbidden() => new(false, default, "You are not allowed to change this item.", true);
}

/// <summary>Anything that belongs to a single user (posts, comments).</summary>
public interface IOwnedResource
{
    string OwnerId { get; }
}

public static class OwnershipOperations
{
    public static readonly OperationAuthorizationRequirement Modify = new() { Name = nameof(Modify) };
}

/// <summary>Resource-based authorization: only the owner may modify an owned resource.</summary>
public class OwnershipHandler : AuthorizationHandler<OperationAuthorizationRequirement, IOwnedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        IOwnedResource resource)
    {
        if (requirement.Name == nameof(OwnershipOperations.Modify)
            && context.User.FindFirstValue(ClaimTypes.NameIdentifier) == resource.OwnerId)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
