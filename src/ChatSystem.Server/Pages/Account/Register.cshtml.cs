using System.ComponentModel.DataAnnotations;
using ChatSystem.Core.Entities;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public RegisterModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public string? SuccessMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Username = Input.Username.Trim();
        Input.DisplayName = Input.DisplayName.Trim();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var usernameExists = await _dbContext.Users.AnyAsync(user => user.Username == Input.Username);
        if (usernameExists)
        {
            ModelState.AddModelError("Input.Username", "用户名已存在。");
            return Page();
        }

        var user = new User
        {
            Username = Input.Username,
            PasswordHash = PasswordHasher.HashPassword(Input.Password),
            DisplayName = Input.DisplayName,
            Role = UserRole.User,
            Status = UserStatus.Pending,
            CreatedAt = DateTime.Now
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        SuccessMessage = "注册申请已提交，请等待管理员审核。";
        Input = new RegisterInput();
        ModelState.Clear();

        return Page();
    }

    public class RegisterInput
    {
        [Display(Name = "用户名")]
        [Required(ErrorMessage = "请输入用户名。")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "用户名长度为 3 到 50 个字符。")]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "昵称")]
        [Required(ErrorMessage = "请输入昵称。")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "昵称长度为 2 到 50 个字符。")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "密码")]
        [Required(ErrorMessage = "请输入密码。")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度至少 6 个字符。")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "确认密码")]
        [Required(ErrorMessage = "请再次输入密码。")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "两次输入的密码不一致。")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
