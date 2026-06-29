using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.Admin;

[Authorize(Roles = "Admin")]
public class PendingUsersModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public PendingUsersModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<User> PendingUsers { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        PendingUsers = await _dbContext.Users
            .Where(user => user.Role == UserRole.User && user.Status == UserStatus.Pending)
            .OrderBy(user => user.CreatedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        var user = await FindPendingUserAsync(id);
        if (user is null)
        {
            StatusMessage = "未找到待审核用户。";
            return RedirectToPage();
        }

        user.Status = UserStatus.Active;
        await _dbContext.SaveChangesAsync();

        StatusMessage = $"已通过用户 {user.Username} 的注册申请。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        var user = await FindPendingUserAsync(id);
        if (user is null)
        {
            StatusMessage = "未找到待审核用户。";
            return RedirectToPage();
        }

        user.Status = UserStatus.Rejected;
        await _dbContext.SaveChangesAsync();

        StatusMessage = $"已拒绝用户 {user.Username} 的注册申请。";
        return RedirectToPage();
    }

    private Task<User?> FindPendingUserAsync(int id)
    {
        return _dbContext.Users.SingleOrDefaultAsync(user =>
            user.Id == id &&
            user.Role == UserRole.User &&
            user.Status == UserStatus.Pending);
    }
}
