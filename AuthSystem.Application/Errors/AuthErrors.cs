using AuthSystem.Domain.Common;

namespace AuthSystem.Application.Errors;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        "Auth.InvalidCredentials",
        "Invalid email or password.",
        401
    );

    public static readonly Error EmailNotConfirmed = new(
        "Auth.EmailNotConfirmed",
        "Email is not confirmed.",
        403
    );

    public static readonly Error EmailAlreadyExists = new(
        "Auth.EmailAlreadyExists",
        "Email is already registered.",
        409
    );

    public static readonly Error EmailAlreadyConfirmed = new(
        "Auth.EmailAlreadyConfirmed",
        "Email is already confirmed.",
        400
    );

    public static readonly Error UserNotFound = new(
        "Auth.UserNotFound",
        "User not found.",
        404
    );

    public static readonly Error InvalidCode = new(
       "Auth.InvalidCode",
       "Invalid or expired confirmation code.",
       400
   );

    public static readonly Error InvalidToken = new(
        "Auth.InvalidToken",
        "Invalid or expired token.",
        401
    );

    public static readonly Error InvalidRefreshToken = new(
        "Auth.InvalidRefreshToken",
        "Refresh token is invalid or expired.",
        401
    );

    public static readonly Error RegistrationFailed = new(
        "Auth.RegistrationFailed",
        "Registration failed.",
        400
    );

    public static readonly Error ResetPasswordFailed = new(
        "Auth.ResetPasswordFailed",
        "Failed to reset password.",
        400
    );
}