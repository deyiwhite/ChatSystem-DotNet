using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.Admin;

[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public UsersModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    public List<User> Users { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadUsersAsync();
    }

    public async Task<IActionResult> OnPostActivateAsync(int id)
    {
        var user = await FindNormalUserAsync(id);
        if (user is null)
        {
            StatusMessage = "未找到普通用户。";
            return RedirectToPage();
        }

        user.Status = UserStatus.Active;
        await _dbContext.SaveChangesAsync();

        StatusMessage = $"已启用用户 {user.Username}。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostBanAsync(int id)
    {
        var user = await FindNormalUserAsync(id);
        if (user is null)
        {
            StatusMessage = "未找到普通用户。";
            return RedirectToPage();
        }

        user.Status = UserStatus.Banned;
        await _dbContext.SaveChangesAsync();

        StatusMessage = $"已禁用用户 {user.Username}，该账号将不能登录。";
        return RedirectToPage();
    }

    private async Task LoadUsersAsync()
    {
        Users = await _dbContext.Users
            .Where(user => user.Role == UserRole.User)
            .OrderBy(user => user.Status)
            .ThenBy(user => user.Username)
            .ToListAsync();
    }

    private Task<User?> FindNormalUserAsync(int id)
    {
        return _dbContext.Users.SingleOrDefaultAsync(user =>
            user.Id == id && user.Role == UserRole.User);
    }
}
