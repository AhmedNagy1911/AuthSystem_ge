using System.Text;
using AuthSystem.Application.DTOs;
using AuthSystem.Application.Errors;
using AuthSystem.Application.Interfaces;
using AuthSystem.Domain.Common;
using AuthSystem.Domain.Entities;
using AuthSystem.Infrastructure.Persistence;
using AuthSystem.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AuthSystem.Infrastructure.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    AppDbContext context,
    JwtService jwtService,
    IEmailService emailService) : IAuthService
{
    private const string EmailConfirmationPurpose = "EmailConfirmation";

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);

        if (!user.EmailConfirmed)
            return Result.Failure<AuthResponse>(AuthErrors.EmailNotConfirmed);

        return Result.Success(await CreateAuthResponse(user));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshTokenRequest request)
    {
        var user = await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens
                .Any(r => r.Token == request.RefreshToken));

        if (user is null)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        var existingToken = user.RefreshTokens.Single(r => r.Token == request.RefreshToken);

        if (!existingToken.IsActive)
            return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken);

        existingToken.RevokedOn = DateTime.UtcNow;

        return Result.Success(await CreateAuthResponse(user));
    }

    public async Task<Result> RevokeRefreshTokenAsync(RefreshTokenRequest request)
    {
        var user = await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens
                .Any(r => r.Token == request.RefreshToken));

        if (user is null)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        var existingToken = user.RefreshTokens.Single(r => r.Token == request.RefreshToken);

        if (!existingToken.IsActive)
            return Result.Failure(AuthErrors.InvalidRefreshToken);

        existingToken.RevokedOn = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> RegisterAsync(RegisterRequest request)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
            return Result.Failure(AuthErrors.EmailAlreadyExists);

        var user = new ApplicationUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return Result.Failure(AuthErrors.RegistrationFailed);

        // بعث الـ OTP Code
        await SendConfirmationCodeAsync(user);

        return Result.Success();
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        if (user.EmailConfirmed)
            return Result.Failure(AuthErrors.EmailAlreadyConfirmed);

        // Verify الـ Code
        var isValid = await userManager.VerifyUserTokenAsync(
            user,
            TokenOptions.DefaultPhoneProvider,
            EmailConfirmationPurpose,
            request.Code
        );

        if (!isValid)
            return Result.Failure(AuthErrors.InvalidCode);

        // Confirm الـ Email يدوياً
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);

        return Result.Success();
    }

    public async Task<Result> ResendConfirmationEmailAsync(ResendConfirmationEmailRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        if (user.EmailConfirmed)
            return Result.Failure(AuthErrors.EmailAlreadyConfirmed);

        await SendConfirmationCodeAsync(user);

        return Result.Success();
    }

    public async Task<Result> ForgetPasswordAsync(ForgetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !user.EmailConfirmed)
            return Result.Success();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var resetLink = $"https://yourfrontend.com/reset-password?email={user.Email}&token={encodedToken}";

        var body = $"""
            <h3>Reset Your Password</h3>
            <p>Click the link below to reset your password:</p>
            <a href="{resetLink}">Reset Password</a>
            <p>This link will expire in 1 hour.</p>
            """;

        await emailService.SendEmailAsync(user.Email!, "Reset Your Password", body);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        var result = await userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);

        return result.Succeeded
            ? Result.Success()
            : Result.Failure(AuthErrors.ResetPasswordFailed);
    }

    // ============ Private Helpers ============

    private async Task<AuthResponse> CreateAuthResponse(ApplicationUser user)
    {
        var jwtToken = jwtService.GenerateToken(user);
        var refreshToken = jwtService.GenerateRefreshToken();

        user.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        return new AuthResponse(
            UserId: user.Id,
            Email: user.Email!,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Token: jwtToken,
            TokenExpiration: DateTime.UtcNow.AddMinutes(15),
            RefreshToken: refreshToken.Token,
            RefreshTokenExpiration: refreshToken.ExpiresOn
        );
    }

    private async Task SendConfirmationCodeAsync(ApplicationUser user)
    {
        // بيولد 6 أرقام تلقائياً
        var code = await userManager.GenerateUserTokenAsync(
            user,
            TokenOptions.DefaultPhoneProvider,
            EmailConfirmationPurpose
        );

        var body = $"""
            <h3>Confirm Your Email</h3>
            <p>Hi {user.FirstName},</p>
            <p>Your confirmation code is:</p>
            <h1 style="letter-spacing: 8px; color: #4F46E5;">{code}</h1>
            <p>This code will expire in 10 minutes.</p>
            <p>If you didn't request this, please ignore this email.</p>
            """;

        await emailService.SendEmailAsync(user.Email!, "Confirm Your Email", body);
    }
}