using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Data;

public static class DatabaseInitializer
{
    private const string AdminPassword = "Admin123!";
    private const string TestPassword = "123456";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");
        await EnsureAdminAsync(dbContext);
        await SeedTestUsersAsync(dbContext);
    }

    private static async Task EnsureAdminAsync(ApplicationDbContext dbContext)
    {
        var admin = await dbContext.Users.SingleOrDefaultAsync(user => user.Username == "admin");
        if (admin is null)
        {
            dbContext.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = PasswordHasher.HashPassword(AdminPassword),
                DisplayName = "管理员",
                Role = UserRole.Admin,
                Status = UserStatus.Active,
                CreatedAt = DateTime.Now
            });
            await dbContext.SaveChangesAsync();
            return;
        }

        if (!PasswordHasher.VerifyPassword(AdminPassword, admin.PasswordHash))
        {
            admin.PasswordHash = PasswordHasher.HashPassword(AdminPassword);
        }

        admin.DisplayName = "管理员";
        admin.Role = UserRole.Admin;
        admin.Status = UserStatus.Active;

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedTestUsersAsync(ApplicationDbContext dbContext)
    {
        var user1 = await EnsureTestUserAsync(dbContext, "user1", "白白");
        var user2 = await EnsureTestUserAsync(dbContext, "user2", "黑黑");
        var user3 = await EnsureTestUserAsync(dbContext, "user3", "小红");

        await EnsureFriendAsync(dbContext, user1.Id, user2.Id);
        await EnsureFriendAsync(dbContext, user2.Id, user1.Id);
        await EnsureFriendAsync(dbContext, user1.Id, user3.Id);
        await EnsureFriendAsync(dbContext, user3.Id, user1.Id);
        await EnsureFriendAsync(dbContext, user2.Id, user3.Id);
        await EnsureFriendAsync(dbContext, user3.Id, user2.Id);

        await dbContext.SaveChangesAsync();
    
    }

    private static async Task<User> EnsureTestUserAsync(
        ApplicationDbContext dbContext,
        string username,
        string displayName)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Username == username);
        if (user is null)
        {
            user = new User
            {
                Username = username,
                PasswordHash = PasswordHasher.HashPassword(TestPassword),
                DisplayName = displayName,
                Role = UserRole.User,
                Status = UserStatus.Active,
                CreatedAt = DateTime.Now
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            return user;
        }

        if (!PasswordHasher.VerifyPassword(TestPassword, user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.HashPassword(TestPassword);
        }

        user.DisplayName = displayName;
        user.Role = UserRole.User;

        return user;
    }

    private static async Task EnsureFriendAsync(
        ApplicationDbContext dbContext,
        int userId,
        int friendId)
    {
        var exists = await dbContext.Friends.AnyAsync(friend =>
            friend.UserId == userId &&
            friend.FriendId == friendId);

        if (exists)
        {
            return;
        }

        dbContext.Friends.Add(new Friend
        {
            UserId = userId,
            FriendId = friendId,
            CreatedAt = DateTime.Now
        });
    }
}
