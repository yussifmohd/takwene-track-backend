using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TrackManagement.UnitTests.TestSupport;

/// <summary>Crafts access tokens (valid and deliberately broken) for API tests.</summary>
public static class TestTokens
{
    public const string SigningKey = "test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz-ABCDEFGHIJ";
    public const string Issuer = "trackmgmt-api-tests";
    public const string Audience = "trackmgmt-web-tests";

    private static readonly string[] AdminRole = ["Admin"];

    public static string Create(
        IEnumerable<string>? roles = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        string signingKey = SigningKey,
        string issuer = Issuer,
        string audience = Audience,
        string algorithm = SecurityAlgorithms.HmacSha256,
        string email = "someone@takwene.test")
    {
        var now = DateTime.UtcNow;
        var nbf = notBefore ?? now.AddMinutes(-1);
        var exp = expires ?? now.AddMinutes(60);
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = nbf,
            NotBefore = nbf,
            Expires = exp,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["email"] = email,
                ["role"] = (roles ?? ["Admin"]).ToArray(),
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), algorithm),
        });
    }

    /// <summary>An unsigned token (<c>alg: none</c>) with otherwise valid claims.</summary>
    public static string CreateUnsigned()
    {
        var now = DateTimeOffset.UtcNow;
        var header = Encode(new { alg = "none", typ = "JWT" });
        var payload = Encode(new
        {
            sub = Guid.NewGuid().ToString(),
            email = "someone@takwene.test",
            role = AdminRole,
            iss = Issuer,
            aud = Audience,
            nbf = now.AddMinutes(-1).ToUnixTimeSeconds(),
            exp = now.AddMinutes(60).ToUnixTimeSeconds(),
        });
        return $"{header}.{payload}.";
    }

    /// <summary>Changes one claim in the payload while keeping the original signature.</summary>
    public static string Tamper(string token)
    {
        var parts = token.Split('.');
        var json = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        var tampered = json.Replace("someone@takwene.test", "attacker@takwene.test", StringComparison.Ordinal);
        return $"{parts[0]}.{Base64UrlEncoder.Encode(tampered)}.{parts[2]}";
    }

    private static string Encode(object value) => Base64UrlEncoder.Encode(JsonSerializer.Serialize(value));
}
