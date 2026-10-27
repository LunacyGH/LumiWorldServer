namespace Lumi.Api.Features.Auth;

// Tên trường khớp AuthModels.cs phía client (JsonUtility so đúng tên, camelCase).

public sealed record RegisterRequest(string? Username, string? Password, string? ConfirmPassword);

public sealed record LoginRequest(string? Username, string? Password);

/// <summary>expiresAt dạng ISO 8601 có offset, client giữ nguyên chuỗi.</summary>
public sealed record AuthResponse(string PlayerId, string Username, string DisplayName, string AccessToken, string ExpiresAt);

public sealed record MeResponse(string PlayerId, string Username, string DisplayName, string Role, string CreatedAt);

/// <summary>Bản ghi dùng cho PasswordHasher; chỉ cần kiểu, không cần dữ liệu.</summary>
public sealed class PlayerAccount;
