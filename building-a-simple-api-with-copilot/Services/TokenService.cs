using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Settings bound from the "Jwt" section of configuration
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    // Symmetric signing key; must be at least 32 bytes for HMAC-SHA256
    public string Key { get; set; } = null!;
    public int ExpiresMinutes { get; set; } = 60;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public interface ITokenService
{
    LoginResponse CreateToken(User user);
}

public class JwtTokenService(JwtOptions options) : ITokenService
{
    public LoginResponse CreateToken(User user)
    {
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(options.ExpiresMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Convert.ToHexString(user.Id)),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.Name)
            ]),
            SigningCredentials = new SigningCredentials(options.GetSigningKey(), SecurityAlgorithms.HmacSha256)
        };

        return new LoginResponse(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
