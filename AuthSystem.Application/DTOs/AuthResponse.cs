namespace AuthSystem.Application.DTOs;

public record AuthResponse(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string Token,
    DateTime TokenExpiration,
    string RefreshToken,
    DateTime RefreshTokenExpiration
);