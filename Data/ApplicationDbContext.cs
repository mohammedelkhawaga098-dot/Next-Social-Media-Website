using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NEXT.Models;

namespace NEXT.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Users are never hard-deleted by the app, and SQL Server forbids multiple cascade
        // paths, so every FK to AspNetUsers is Restrict. Only post children cascade from Post.
        b.Entity<Post>(e =>
        {
            e.HasOne(p => p.Author).WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.SharedPost).WithMany(p => p.Shares).HasForeignKey(p => p.SharedPostId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            e.HasIndex(p => p.CreatedAt);
            e.HasIndex(p => p.AuthorId);
        });

        b.Entity<Comment>(e =>
        {
            e.HasOne(c => c.Post).WithMany(p => p.Comments).HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => new { c.PostId, c.CreatedAt });
        });

        b.Entity<PostLike>(e =>
        {
            e.HasOne(l => l.Post).WithMany(p => p.Likes).HasForeignKey(l => l.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => new { l.PostId, l.UserId }).IsUnique();
        });

        b.Entity<Friendship>(e =>
        {
            e.HasOne(f => f.Requester).WithMany().HasForeignKey(f => f.RequesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Addressee).WithMany().HasForeignKey(f => f.AddresseeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();
            e.HasIndex(f => new { f.AddresseeId, f.Status });
        });

        b.Entity<Follow>(e =>
        {
            e.HasKey(f => new { f.FollowerId, f.FollowingId });
            e.HasOne(f => f.Follower).WithMany().HasForeignKey(f => f.FollowerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Following).WithMany().HasForeignKey(f => f.FollowingId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Message>(e =>
        {
            e.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Recipient).WithMany().HasForeignKey(m => m.RecipientId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.SenderId, m.RecipientId, m.SentAt });
            e.HasIndex(m => new { m.RecipientId, m.IsRead });
        });

        b.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(n => n.Actor).WithMany().HasForeignKey(n => n.ActorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        });

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(80);
            e.HasIndex(u => u.FullName);
        });
    }
}
