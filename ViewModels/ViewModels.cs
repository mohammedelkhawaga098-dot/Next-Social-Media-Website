using System.ComponentModel.DataAnnotations;
using NEXT.Models;

namespace NEXT.ViewModels;

// ---------- Shared ----------
public record AvatarVm(string? Path, string Name, int Size = 40, string? UserId = null, bool ShowStatus = false);

public enum RelationState { None, Self, Friends, RequestSent, RequestReceived }

public class LayoutData
{
    public ApplicationUser? User { get; set; }
    public string Theme => User?.Theme == "light" ? "light" : "dark";
    public int UnreadNotifications { get; set; }
    public int UnreadMessages { get; set; }
    public int PendingRequests { get; set; }
}

public class EmptyStateVm
{
    public string Icon { get; set; } = "fa-regular fa-folder-open";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}

// ---------- Auth ----------
public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your username or email."), Display(Name = "Username or email")]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your name."), StringLength(80, MinimumLength = 2), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "Use letters, numbers, dots and underscores only.")]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ---------- Posts ----------
public class SharedPostViewModel
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorUserName { get; set; } = "";
    public string? AuthorAvatar { get; set; }
    public string Content { get; set; } = "";
    public string? ImagePath { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PostViewModel
{
    public int Id { get; set; }
    public string AuthorId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorUserName { get; set; } = "";
    public string? AuthorAvatar { get; set; }
    public string Content { get; set; } = "";
    public string? ImagePath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public bool LikedByMe { get; set; }
    public bool IsOwner { get; set; }
    public bool IsShare { get; set; }
    public SharedPostViewModel? Shared { get; set; }
}

public class CommentViewModel
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorUserName { get; set; } = "";
    public string? AuthorAvatar { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsOwner { get; set; }
}

public class CommentsVm
{
    public int PostId { get; set; }
    public List<CommentViewModel> Comments { get; set; } = new();
}

public enum FeedScope { Network, Everyone }

public class PostListViewModel
{
    public List<PostViewModel> Posts { get; set; } = new();
    public bool HasMore { get; set; }
    public int Page { get; set; }
    public FeedScope Scope { get; set; }
    public string? UserName { get; set; }
    public bool IsOwnProfile { get; set; }
}

// ---------- People ----------
public class UserCardViewModel
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? Avatar { get; set; }
    public string? Bio { get; set; }
    public RelationState State { get; set; }
}

public class ProfileViewModel
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? Bio { get; set; }
    public string? Avatar { get; set; }
    public string? Cover { get; set; }
    public DateTime JoinedAt { get; set; }
    public int PostCount { get; set; }
    public int FriendCount { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public RelationState State { get; set; }
    public bool IsFollowing { get; set; }
    public List<UserCardViewModel> FriendPreview { get; set; } = new();
}

public class FriendsPageViewModel
{
    public List<UserCardViewModel> Friends { get; set; } = new();
    public List<UserCardViewModel> Suggestions { get; set; } = new();
}

public class RequestsPageViewModel
{
    public List<UserCardViewModel> Incoming { get; set; } = new();
    public List<UserCardViewModel> Outgoing { get; set; } = new();
}

public class SearchViewModel
{
    public string Query { get; set; } = "";
    public List<UserCardViewModel> Results { get; set; } = new();
}

// ---------- Notifications ----------
public class NotificationViewModel
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public string Url { get; set; } = "/";
    public string ActorName { get; set; } = "";
    public string? ActorAvatar { get; set; }
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ---------- Messages ----------
public class MessageViewModel
{
    public int Id { get; set; }
    public string SenderId { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ImagePath { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsMine { get; set; }
    public bool IsRead { get; set; }
}

public class ConversationViewModel
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? Avatar { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool LastMessageMine { get; set; }
    public int Unread { get; set; }
}

public class MessagesPageViewModel
{
    public List<ConversationViewModel> Conversations { get; set; } = new();
    public ConversationViewModel? Active { get; set; }
    public bool CanSend { get; set; }
    public List<MessageViewModel> Messages { get; set; } = new();
}

// ---------- Settings ----------
public class EditProfileViewModel
{
    [Required, StringLength(80, MinimumLength = 2), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "Use letters, numbers, dots and underscores only.")]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [StringLength(300), Display(Name = "Bio")]
    public string? Bio { get; set; }

    public IFormFile? ProfilePicture { get; set; }
    public IFormFile? CoverPhoto { get; set; }

    public string? CurrentAvatar { get; set; }
    public string? CurrentCover { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class SettingsPageViewModel
{
    public EditProfileViewModel Profile { get; set; } = new();
    public ChangePasswordViewModel Password { get; set; } = new();
    public string Theme { get; set; } = "dark";
}
