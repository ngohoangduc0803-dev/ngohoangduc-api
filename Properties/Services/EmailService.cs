
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;

namespace ngohoangduc.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendOtpAsync(string email, string otp)
    {
        var apiKey = _configuration["Resend:ApiKey"];

        var fromEmail = _configuration["Resend:FromEmail"]
            ?? "onboarding@resend.dev";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Chua cau hinh Resend:ApiKey"
            );
        }

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(otp))
        {
            throw new ArgumentException(
                "Email hoac OTP khong hop le"
            );
        }

        var safeOtp = HtmlEncoder.Default.Encode(otp);

        var html = $"""
            <div style="font-family:Arial,sans-serif;
                        max-width:480px;margin:auto;
                        padding:24px">
                <h2>NGOHOANGDUC - Xac thuc dang nhap</h2>

                <p>Ma xac thuc OTP cua ban la:</p>

                <div style="font-size:32px;
                            font-weight:bold;
                            letter-spacing:6px;
                            padding:20px;
                            background:#f3f4f6;
                            text-align:center">
                    {safeOtp}
                </div>

                <p>Khong chia se ma nay voi bat ky ai.</p>

                <p>Neu ban khong yeu cau dang nhap,
                   hay bo qua email nay.</p>
            </div>
            """;

        var payload = new
        {
            from = $"NGOHOANGDUC OTP <{fromEmail}>",
            to = new[] { email },
            subject = "Ma xac thuc OTP - NGOHOANGDUC",
            html
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.resend.com/emails"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                apiKey
            );

        request.Content = JsonContent.Create(payload);

        using var response = await Http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                "Resend khong gui duoc email OTP. " +
                $"HTTP status: {(int)response.StatusCode}"
            );
        }
    }
}
