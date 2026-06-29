using System.Security.Claims;
using System.Text.Encodings.Web;
using ChatSystem.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ChatSystem.Server.Authentication;

public sealed class DesktopTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DesktopTokenService _tokenService;

    public DesktopTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DesktopTokenService tokenService)
        : base(options, logger, encoder)
    {
        _tokenService = tokenService;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = GetToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var result = _tokenService.ValidateToken(token);
        if (result is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid desktop token."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.Username),
            new(ClaimTypes.Role, result.Role),
            new("DisplayName", result.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, DesktopTokenAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, DesktopTokenAuthenticationDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string? GetToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        if (Request.Path.StartsWithSegments("/chatHub") &&
            Request.Query.TryGetValue("access_token", out var accessToken))
        {
            return accessToken.ToString();
        }

        return null;
    }
}
