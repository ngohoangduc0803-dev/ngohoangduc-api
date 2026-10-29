
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ngohoangduc.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendOtpAsync(
        string recipientEmail,
        string otp)
    {
        var senderEmail =
            _configuration["Smtp:Email"];

        var appPassword =
            _configuration["Smtp:AppPassword"];

        if (string.IsNullOrWhiteSpace(senderEmail) ||
            string.IsNullOrWhiteSpace(appPassword))
        {
            throw new InvalidOperationException(
                "Chua cau hinh Gmail SMTP trong User Secrets."
            );
        }

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                "Smart Attendance",
                senderEmail
            )
        );

        message.To.Add(
            MailboxAddress.Parse(recipientEmail)
        );

        message.Subject =
            "Ma OTP dang nhap Smart Attendance";

        message.Body = new TextPart("plain")
        {
            Text =
                $"Ma xac thuc dang nhap cua ban la: {otp}\n\n" +
                "Ma OTP co thoi han su dung ngan.\n" +
                "Khong chia se ma nay voi nguoi khac."
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            "smtp.gmail.com",
            587,
            SecureSocketOptions.StartTls
        );

        await client.AuthenticateAsync(
            senderEmail,
            appPassword
        );

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
