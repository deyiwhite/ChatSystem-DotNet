using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Data;

public class ApplicationDbContext : DbContext
{
    private static readonly DateTime SeedCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] AdminPasswordSalt = Convert.FromBase64String("Q2hhdFN5c3RlbUFkbWluIQ==");

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();

    public DbSet<Friend> Friends => Set<Friend>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<ChatGroup> ChatGroups => Set<ChatGroup>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    public DbSet<GroupMessage> GroupMessages => Set<GroupMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Username).HasMaxLength(50).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(50).IsRequired();
            entity.HasIndex(user => user.Username).IsUnique();

            entity.HasData(new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = PasswordHasher.HashPassword("Admin123!", AdminPasswordSalt),
                DisplayName = "管理员",
                Role = UserRole.Admin,
                Status = UserStatus.Active,
                CreatedAt = SeedCreatedAt
            });
        });

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.HasIndex(request => new { request.FromUserId, request.ToUserId, request.Status });

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(request => request.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(request => request.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Friend>(entity =>
        {
            entity.HasIndex(friend => new { friend.UserId, friend.FriendId }).IsUnique();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(friend => friend.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(friend => friend.FriendId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.Property(message => message.Content).HasMaxLength(1000).IsRequired();
            entity.Property(message => message.AttachmentFileName).HasMaxLength(260);
            entity.Property(message => message.AttachmentStoredName).HasMaxLength(100);
            entity.HasIndex(message => new { message.FromUserId, message.ToUserId, message.SentAt });

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(message => message.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(message => message.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatGroup>(entity =>
        {
            entity.Property(group => group.Name).HasMaxLength(50).IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(group => group.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasIndex(member => new { member.GroupId, member.UserId }).IsUnique();

            entity.HasOne<ChatGroup>()
                .WithMany()
                .HasForeignKey(member => member.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GroupMessage>(entity =>
        {
            entity.Property(message => message.Content).HasMaxLength(1000).IsRequired();
            entity.Property(message => message.AttachmentFileName).HasMaxLength(260);
            entity.Property(message => message.AttachmentStoredName).HasMaxLength(100);
            entity.HasIndex(message => new { message.GroupId, message.SentAt });

            entity.HasOne<ChatGroup>()
                .WithMany()
                .HasForeignKey(message => message.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(message => message.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
