using System.Security.Claims;
using System.Text;
using ContactTogetherApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ContactTogetherApi.Auth;

public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateToken(TblEmployee employee)
    {
        var tokenId = Guid.NewGuid().ToString("N");
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_settings.ExpiresMinutes);

        var claims = new List<Claim>
        {
            // TblEmployee has no NOT NULL constraints any more; Claim throws on a null value.
            new(JwtRegisteredClaimNames.Sub, employee.Id ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(JwtRegisteredClaimNames.Name, employee.UserName ?? string.Empty),
            new(AuthClaimTypes.Role, employee.RoleId ?? string.Empty),
            new(AuthClaimTypes.Language, employee.DefaultLanguage ?? string.Empty),
        };

        if (!string.IsNullOrEmpty(employee.OrganizationId))
        {
            claims.Add(new Claim(AuthClaimTypes.OrganizationId, employee.OrganizationId));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = _credentials,
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, tokenId, expiresAt);
    }
}
