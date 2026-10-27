using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lumi.Api.Features.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "lumi";
    public string Audience { get; set; } = "lumi-client";

    /// <summary>Khóa ký HS256, ít nhất 32 ký tự. Không commit khóa thật, đặt qua biến môi trường Jwt__Key.</summary>
    public string Key { get; set; } = "";

    /// <summary>Token sống 7 ngày để người chơi vẫn chơi offline được một tuần rồi mới phải đăng nhập lại.</summary>
    public int LifetimeHours { get; set; } = 168;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public sealed class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid playerId, string username, string role)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(_options.LifetimeHours);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", playerId.ToString()),
                new Claim("name", username),
                new Claim("role", role)
            }),
            SigningCredentials = new SigningCredentials(_options.SigningKey(), SecurityAlgorithms.HmacSha256)
        };

        return (_handler.CreateToken(descriptor), expiresAt);
    }
}
