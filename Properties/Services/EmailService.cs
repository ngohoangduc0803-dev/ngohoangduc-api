
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ngohoangduc.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;

    private readonly HttpClient _http;

    public EmailService(IConfiguration configuration, HttpClient http)
    {
        _configuration = configuration;
        _http = http;
    }

    public async Task SendOtpAsync(string email, string otp)
    {
        var apiKey = _configuration["Resend:ApiKey"];

        var fromEmail = _configuration["Resend:FromEmail"]
            ?? "onboarding@resend.dev";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new EmailDeliveryException(
                "EMAIL_NOT_CONFIGURED",
                "Dich vu gui OTP chua duoc cau hinh. Vui long lien he quan tri vien."
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
                apiKey.Trim()
            );

        request.Content = JsonContent.Create(payload);

        using var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            // Inspect provider errors without returning their raw text, email addresses or secrets.
            var providerMessage = "";
            try
            {
                using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (error.RootElement.ValueKind == JsonValueKind.Object
                    && error.RootElement.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    providerMessage = message.GetString() ?? "";
                }
            }
            catch (JsonException) { }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden
                && providerMessage.Contains("testing emails", StringComparison.OrdinalIgnoreCase))
            {
                throw new EmailDeliveryException("EMAIL_TEST_MODE",
                    "Resend dang o che do thu, chua the gui OTP den email nay. "
                    + "Quan tri vien can xac minh ten mien gui email tren Resend.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden
                && providerMessage.Contains("domain", StringComparison.OrdinalIgnoreCase)
                && providerMessage.Contains("not verified", StringComparison.OrdinalIgnoreCase))
            {
                throw new EmailDeliveryException("EMAIL_DOMAIN_NOT_VERIFIED",
                    "Ten mien gui email chua duoc xac minh tren Resend. "
                    + "Vui long lien he quan tri vien.");
            }

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
            {
                throw new EmailDeliveryException("EMAIL_PROVIDER_AUTH_FAILED",
                    "Cau hinh quyen gui email Resend khong hop le. "
                    + "Vui long lien he quan tri vien.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new EmailDeliveryException("EMAIL_RATE_LIMITED",
                    "Dich vu email dang gioi han luot gui. Vui long thu lai sau.");
            }

            throw new EmailDeliveryException("EMAIL_SERVICE_UNAVAILABLE",
                "Dich vu email tam thoi khong gui duoc OTP. Vui long thu lai sau.");
        }
    }
}
