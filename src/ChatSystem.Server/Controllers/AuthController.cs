using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly DesktopTokenService _tokenService;

    public AuthController(ApplicationDbContext dbContext, DesktopTokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var username = request.Username.Trim();
        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Username == username);
        if (user is null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ErrorResponse("用户名或密码错误。"));
        }

        if (user.Role != UserRole.User)
        {
            return Unauthorized(new ErrorResponse("桌面端只允许普通用户登录。"));
        }

        if (user.Status != UserStatus.Active)
        {
            return Unauthorized(new ErrorResponse("账号当前不能登录。"));
        }

        var token = _tokenService.CreateToken(user);
        return new LoginResponse(token, user.Id, user.Username, user.DisplayName);
    }
}

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string Token,
    int UserId,
    string Username,
    string DisplayName);

public sealed record ErrorResponse(string Message);
