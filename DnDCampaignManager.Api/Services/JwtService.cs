using DnDCampingManager.Api.Models;
using DnDCampingManager.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DnDCampingManager.Api.Services;

public class JwtService : IJwtService
{
    private readonly JwtOptions _jwt;

    public JwtService(IOptions<JwtOptions> jwtOptions)
    {
        _jwt = jwtOptions.Value;
    }

    public string GenerateToken(User user)
    {
        var active = _jwt.SigningKeys.First(); 

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(active.Key))
        {
            KeyId = active.Kid
        };

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        // ensure kid shows in header
        token.Header["kid"] = active.Kid;

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
