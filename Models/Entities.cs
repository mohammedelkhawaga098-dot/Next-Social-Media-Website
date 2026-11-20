using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using NEXT.Helpers;

namespace NEXT.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(80)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Bio { get; set; }

    public string? ProfilePicturePath { get; set; }
    public string? CoverPhotoPath { get; set; }

    /// <summary>"dark" (default) or "light". Stored per user so it follows them across devices.</summary>
    [StringLength(10)]
    public string Theme { get; set; } = "dark";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

public class Post : IOwnedResource
{
    public int Id { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Set when this post is a share of another post.</summary>
    public int? SharedPostId { get; set; }
    public Post? SharedPost { get; set; }

    public ICollection<Post> Shares { get; set; } = new List<Post>();
    public ICollection<PostLike> Likes { get; set; } = new List<PostLike>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    string IOwnedResource.OwnerId => AuthorId;
}

public class Comment : IOwnedResource
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    [Required, StringLength(1000)]
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    string IOwnedResource.OwnerId => AuthorId;
}

public class PostLike
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum FriendshipStatus { Pending = 0, Accepted = 1 }

public class Friendship
{
    public int Id { get; set; }
    public string RequesterId { get; set; } = string.Empty;
    public ApplicationUser Requester { get; set; } = null!;
    public string AddresseeId { get; set; } = string.Empty;
    public ApplicationUser Addressee { get; set; } = null!;
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}

public class Follow
{
    public string FollowerId { get; set; } = string.Empty;
    public ApplicationUser Follower { get; set; } = null!;
    public string FollowingId { get; set; } = string.Empty;
    public ApplicationUser Following { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Message
{
    public int Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;
    public string RecipientId { get; set; } = string.Empty;
    public ApplicationUser Recipient { get; set; } = null!;

    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public enum NotificationType
{
    FriendRequest = 0,
    FriendAccepted = 1,
    PostLike = 2,
    PostComment = 3,
    Message = 4,
    PostShare = 5
}

public class Notification
{
    public int Id { get; set; }

    /// <summary>The user who receives the notification.</summary>
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    /// <summary>The user who caused it.</summary>
    public string ActorId { get; set; } = string.Empty;
    public ApplicationUser Actor { get; set; } = null!;

    public NotificationType Type { get; set; }
    public int? PostId { get; set; }

    [StringLength(300)]
    public string Text { get; set; } = string.Empty;
    [StringLength(300)]
    public string Url { get; set; } = "/";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
