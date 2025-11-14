using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Evermail.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly ECDsa _ecdsaKey;
    private readonly EvermailDbContext _context;

    public JwtTokenService(string issuer, string audience, ECDsa ecdsaKey, EvermailDbContext context)
    {
        _issuer = issuer;
        _audience = audience;
        _ecdsaKey = ecdsaKey;
        _context = context;
    }

    public Task<string> GenerateTokenAsync(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("tenant_id", user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        // Add roles
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new ECDsaSecurityKey(_ecdsaKey),
                SecurityAlgorithms.EcdsaSha256
            )
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return Task.FromResult(tokenHandler.WriteToken(token));
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = new ECDsaSecurityKey(_ecdsaKey),
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public async Task<TokenPair> GenerateTokenPairAsync(ApplicationUser user, IList<string> roles, string? ipAddress = null)
    {
        // Generate access token (15 minutes)
        var accessToken = await GenerateTokenAsync(user, roles);
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(accessToken);
        var jti = jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        
        // Generate refresh token (30 days)
        var refreshTokenString = GenerateRefreshTokenString();
        var refreshTokenHash = HashToken(refreshTokenString);
        
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = refreshTokenHash,
            JwtId = jti,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };
        
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
        
        return new TokenPair(
            accessToken, 
            refreshTokenString, 
            DateTime.UtcNow.AddMinutes(15),
            refreshToken.ExpiresAt
        );
    }

    public async Task<TokenPair?> RefreshTokenAsync(string refreshToken, string? ipAddress = null)
    {
        var tokenHash = HashToken(refreshToken);
        
        // Find the refresh token in database
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
        
        if (storedToken == null || !storedToken.IsActive)
        {
            return null; // Invalid or expired token
        }
        
        // Get user roles
        var user = storedToken.User;
        var roles = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
            .ToListAsync();
        
        // Revoke old refresh token (token rotation for security)
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokeReason = "Replaced by new token";
        storedToken.UsedAt = DateTime.UtcNow;
        storedToken.UsedByIp = ipAddress;
        
        // Generate new token pair
        var newTokenPair = await GenerateTokenPairAsync(user, roles, ipAddress);
        
        // Link old token to new token
        storedToken.ReplacedByTokenId = Guid.Parse(HashToken(newTokenPair.RefreshToken));
        
        await _context.SaveChangesAsync();
        
        return newTokenPair;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, string reason, string? ipAddress = null)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
        
        if (storedToken == null || storedToken.RevokedAt != null)
        {
            return; // Already revoked or doesn't exist
        }
        
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokeReason = reason;
        storedToken.UsedByIp = ipAddress;
        
        await _context.SaveChangesAsync();
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, string reason)
    {
        var userTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();
        
        foreach (var token in userTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokeReason = reason;
        }
        
        await _context.SaveChangesAsync();
    }

    private static string GenerateRefreshTokenString()
    {
        // Generate cryptographically secure random token (64 bytes = 512 bits)
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        // Hash the token for storage (one-way hash, can't be reversed)
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }
}

