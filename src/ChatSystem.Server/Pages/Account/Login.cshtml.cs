using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.Account;

public class LoginModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public LoginModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? StatusMessage { get; private set; }

    // 新增：标记是否是管理员登录模式（仅用于页面回显）
    public bool IsAdminLogin { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectByRole();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // 新增：判断是否是管理员登录
        IsAdminLogin = Request.Form["IsAdminLogin"] == "true";

        Input.Username = Input.Username.Trim();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Username == Input.Username);
        if (user is null || !PasswordHasher.VerifyPassword(Input.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "用户名或密码错误。");
            return Page();
        }

        if (user.Status != UserStatus.Active)
        {
            StatusMessage = user.Status switch
            {
                UserStatus.Pending => "账号正在等待管理员审核，暂时不能登录。",
                UserStatus.Rejected => "注册申请未通过，不能登录。",
                UserStatus.Banned => "账号已被禁用，不能登录。",
                _ => "账号状态异常，不能登录。"
            };
            return Page();
        }

        // 新增：管理员登录模式
        if (IsAdminLogin)
        {
            if (user.Role != UserRole.Admin)
            {
                ModelState.AddModelError(string.Empty, "该账号不是管理员账号。");
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("DisplayName", user.DisplayName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var properties = new AuthenticationProperties
            {
                IsPersistent = Input.RememberMe
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

            return RedirectToPage("/Admin/Index");
        }

        // 新增：普通用户登录，禁止管理员角色
        if (user.Role == UserRole.Admin)
        {
            ModelState.AddModelError(string.Empty, "请使用管理员登录入口。");
            return Page();
        }

        var userClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("DisplayName", user.DisplayName)
        };

        var userIdentity = new ClaimsIdentity(userClaims, CookieAuthenticationDefaults.AuthenticationScheme);
        var userPrincipal = new ClaimsPrincipal(userIdentity);
        var userProperties = new AuthenticationProperties
        {
            IsPersistent = Input.RememberMe
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, userPrincipal, userProperties);

        return RedirectByRole(user.Role);
    }

    private IActionResult RedirectByRole(UserRole? role = null)
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        var currentRole = role?.ToString();
        if (currentRole is null && User.Identity?.IsAuthenticated == true)
        {
            currentRole = User.FindFirst(ClaimTypes.Role)?.Value;
        }

        if (currentRole == UserRole.Admin.ToString())
        {
            return RedirectToPage("/Admin/Index");
        }

        return RedirectToPage("/Index");
    }

    public class LoginInput
    {
        [Display(Name = "用户名")]
        [Required(ErrorMessage = "请输入用户名。")]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "密码")]
        [Required(ErrorMessage = "请输入密码。")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "保持登录")]
        public bool RememberMe { get; set; }
    }
}