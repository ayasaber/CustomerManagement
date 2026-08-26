using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CustomerManagement.Api.Contracts.Auth;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CustomerManagement.Api.Infrastructure.Auth;

public sealed class JwtTokenService(
    IOptions<JwtOptions> jwtOptions,
    CustomerManagementDbContext dbContext,
    UserManager<ApplicationUser> userManager)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<TokenResponse> IssueTokensAsync(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var accessTokenMinutes = await ResolveIntSettingAsync(
            SystemSettingKeys.AuthAccessTokenMinutes,
            _jwtOptions.AccessTokenMinutes,
            minValue: 1,
            maxValue: 1440,
            cancellationToken);
        var refreshTokenDays = await ResolveIntSettingAsync(
            SystemSettingKeys.AuthRefreshTokenDays,
            _jwtOptions.RefreshTokenDays,
            minValue: 1,
            maxValue: 365,
            cancellationToken);

        var accessTokenExpiresAtUtc = now.AddMinutes(accessTokenMinutes);
        var refreshTokenExpiresAtUtc = now.AddDays(refreshTokenDays);

        var permissions = await ResolvePermissionsAsync(roles, cancellationToken);

        var accessToken = BuildAccessToken(user, roles, permissions, accessTokenExpiresAtUtc);
        var refreshToken = GenerateRefreshToken();
        var refreshTokenHash = Hash(refreshToken);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAtUtc = refreshTokenExpiresAtUtc,
            CreatedAtUtc = now,
            CreatedByIp = ipAddress
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TokenResponse(
            accessToken,
            accessTokenExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc,
            user.Id.ToString(),
            user.Email ?? string.Empty,
            roles,
            permissions);
    }

    public async Task<TokenResponse?> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var refreshTokenHash = Hash(refreshToken);
        var now = DateTime.UtcNow;

        var existingRefreshToken = await dbContext.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash, cancellationToken);

        if (existingRefreshToken is null ||
            existingRefreshToken.RevokedAtUtc.HasValue ||
            existingRefreshToken.ExpiresAtUtc <= now ||
            !existingRefreshToken.User.IsActive)
        {
            return null;
        }

        existingRefreshToken.RevokedAtUtc = now;

        var roles = await userManager.GetRolesAsync(existingRefreshToken.User);
        var roleList = roles.ToList();
        var permissions = await ResolvePermissionsAsync(roleList, cancellationToken);

        var accessTokenMinutes = await ResolveIntSettingAsync(
            SystemSettingKeys.AuthAccessTokenMinutes,
            _jwtOptions.AccessTokenMinutes,
            minValue: 1,
            maxValue: 1440,
            cancellationToken);
        var refreshTokenDays = await ResolveIntSettingAsync(
            SystemSettingKeys.AuthRefreshTokenDays,
            _jwtOptions.RefreshTokenDays,
            minValue: 1,
            maxValue: 365,
            cancellationToken);

        var accessTokenExpiresAtUtc = now.AddMinutes(accessTokenMinutes);
        var refreshTokenExpiresAtUtc = now.AddDays(refreshTokenDays);

        var newAccessToken = BuildAccessToken(existingRefreshToken.User, roleList, permissions, accessTokenExpiresAtUtc);
        var newRefreshToken = GenerateRefreshToken();
        var newRefreshTokenHash = Hash(newRefreshToken);

        existingRefreshToken.ReplacedByTokenHash = newRefreshTokenHash;

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingRefreshToken.UserId,
            TokenHash = newRefreshTokenHash,
            ExpiresAtUtc = refreshTokenExpiresAtUtc,
            CreatedAtUtc = now,
            CreatedByIp = ipAddress
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TokenResponse(
            newAccessToken,
            accessTokenExpiresAtUtc,
            newRefreshToken,
            refreshTokenExpiresAtUtc,
            existingRefreshToken.User.Id.ToString(),
            existingRefreshToken.User.Email ?? string.Empty,
            roleList,
            permissions);
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var refreshTokenHash = Hash(refreshToken);
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash, cancellationToken);

        if (token is null || token.RevokedAtUtc.HasValue)
        {
            return false;
        }

        token.RevokedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private string BuildAccessToken(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private async Task<IReadOnlyList<string>> ResolvePermissionsAsync(
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return [];
        }

        var normalizedRoles = roles
            .Select(role => role.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissions = await (
                from role in dbContext.Roles.AsNoTracking()
                join rolePermission in dbContext.RolePermissions.AsNoTracking() on role.Id equals rolePermission.RoleId
                join permission in dbContext.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
                where role.Name != null && normalizedRoles.Contains(role.Name)
                select permission.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        return permissions;
    }

    private async Task<int> ResolveIntSettingAsync(
        string key,
        int fallback,
        int minValue,
        int maxValue,
        CancellationToken cancellationToken)
    {
        var normalizedFallback = Math.Clamp(fallback, minValue, maxValue);
        var value = await dbContext.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (value is null || !int.TryParse(value, out var parsed))
        {
            return normalizedFallback;
        }

        return Math.Clamp(parsed, minValue, maxValue);
    }
}
