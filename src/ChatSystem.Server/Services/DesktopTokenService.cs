using System.Security.Cryptography;
using System.Text;
using ChatSystem.Core.Entities;

namespace ChatSystem.Server.Services;

public sealed class DesktopTokenService
{
    private const string Secret = "ChatSystem.Desktop.Token.Secret.2026";

    public string CreateToken(User user)
    {
        var displayName = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.DisplayName));
        var payload = $"{user.Id}|{user.Username}|{user.Role}|{displayName}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signaturePart = Sign(payloadPart);

        return $"{payloadPart}.{signaturePart}";
    }

    public DesktopTokenValidationResult? ValidateToken(string token)
    {
        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var expectedSignature = Sign(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(parts[1])))
        {
            return null;
        }

        var payloadText = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        var payloadParts = payloadText.Split('|');
        if (payloadParts.Length != 5 || !int.TryParse(payloadParts[0], out var userId))
        {
            return null;
        }

        var displayName = Encoding.UTF8.GetString(Convert.FromBase64String(payloadParts[3]));
        return new DesktopTokenValidationResult(userId, payloadParts[1], payloadParts[2], displayName);
    }

    private static string Sign(string payloadPart)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart)));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        var padding = (4 - base64.Length % 4) % 4;
        return Convert.FromBase64String(base64 + new string('=', padding));
    }
}

public sealed record DesktopTokenValidationResult(
    int UserId,
    string Username,
    string Role,
    string DisplayName);
