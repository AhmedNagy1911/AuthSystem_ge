using AuthSystem.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace AuthSystem.Infrastructure.Services;

public class EmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var emailSettings = configuration.GetSection("EmailSettings");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            emailSettings["SenderName"],
            emailSettings["SenderEmail"]
        ));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        message.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            emailSettings["SmtpHost"],
            int.Parse(emailSettings["SmtpPort"]!),
            SecureSocketOptions.StartTls
        );

        await client.AuthenticateAsync(
            emailSettings["SenderEmail"],
            emailSettings["SenderPassword"]
        );

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}