// AuthSystem.Infrastructure/Services/AuthService.cs
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

        await SendConfirmationEmailAsync(user);

        return Result.Success();
    }

    public async Task<Result> ConfirmEmailAsync(string userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        if (user.EmailConfirmed)
            return Result.Failure(AuthErrors.EmailAlreadyConfirmed);

        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        var result = await userManager.ConfirmEmailAsync(user, decodedToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Failure(AuthErrors.InvalidToken);
    }

    public async Task<Result> ResendConfirmationEmailAsync(ResendConfirmationEmailRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        if (user.EmailConfirmed)
            return Result.Failure(AuthErrors.EmailAlreadyConfirmed);

        await SendConfirmationEmailAsync(user);

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

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var confirmLink = $"https://yourfrontend.com/confirm-email?userId={user.Id}&token={encodedToken}";

        var body = $"""
            <h3>Confirm Your Email</h3>
            <p>Hi {user.FirstName}, click the link below to confirm your email:</p>
            <a href="{confirmLink}">Confirm Email</a>
            """;

        await emailService.SendEmailAsync(user.Email!, "Confirm Your Email", body);
    }
}