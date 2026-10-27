using System.Security.Claims;

namespace Lumi.Api.Infrastructure;

public static class PlayerClaims
{
    /// <summary>
    /// Id người chơi lấy từ claim "sub" của token. Chỉ gọi trong endpoint có RequireAuthorization.
    /// </summary>
    public static Guid PlayerId(this ClaimsPrincipal user)
    {
        string? sub = user.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out Guid id))
            throw new InvalidOperationException("Token không có claim sub hợp lệ.");
        return id;
    }
}
