using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Security;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "skillpilot";
    public int ExpiryDays { get; set; } = 14;
}

public class JwtService(Microsoft.Extensions.Options.IOptions<JwtOptions> options)
{
    private readonly JwtOptions _o = options.Value;

    public static SymmetricSecurityKey KeyFrom(string key) => new(Encoding.UTF8.GetBytes(key));

    public string Create(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
        };
        var token = new JwtSecurityToken(
            _o.Issuer, _o.Issuer, claims,
            expires: DateTime.UtcNow.AddDays(_o.ExpiryDays),
            signingCredentials: new SigningCredentials(KeyFrom(_o.Key), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
