using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CustomerManagement.Gateway.Tests.Infrastructure;

public static class TestJwtFactory
{
    public const string Issuer = "CustomerManagement.Api";
    public const string Audience = "CustomerManagement.Ui";
    public const string SigningKey = "DevOnly-ReplaceThis-With-A-Strong-Secret-Key-12345";

    public static string CreateToken(
        string userId,
        string email,
        IEnumerable<string>? roles = null,
        IEnumerable<string>? permissions = null,
        DateTime? expiresAtUtc = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles is not null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }
        }

        if (permissions is not null)
        {
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expiresAtUtc ?? DateTime.UtcNow.AddMinutes(20),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
