namespace AuthSystem.Application.DTOs;

public record ConfirmEmailRequest(
    string UserId,
    string Code
);