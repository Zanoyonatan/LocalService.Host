
using Microsoft.AspNetCore.Http;

namespace LocalService.Host.Infra;

public static class JwtHeaderValidator
{
    public static bool TryValidateAuthorizationHeader(HttpRequest request, out string error)
    {
        error = "missing_authorization_header";

        if (!request.Headers.TryGetValue("Authorization", out var authValues))
            return false;

        var auth = authValues.ToString();
        if (string.IsNullOrWhiteSpace(auth))
        {
            error = "empty_authorization_header";
            return false;
        }

        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            error = "authorization_not_bearer";
            return false;
        }

        var token = auth.Substring("Bearer ".Length).Trim();
        if (!LooksLikeJwt(token))
        {
            error = "token_not_jwt_format";
            return false;
        }

        error = "";
        return true;
    }

    // JWT format: header.payload.signature (3 parts)
    private static bool LooksLikeJwt(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return false;
        return parts.All(p => p.Length > 0);
    }
}
