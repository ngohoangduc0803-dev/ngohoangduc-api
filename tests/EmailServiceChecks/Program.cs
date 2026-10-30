using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ngohoangduc.Services;

var checks = 0;

IConfiguration Configuration(string? apiKey) => new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Resend:ApiKey"] = apiKey,
        ["Resend:FromEmail"] = "noreply@verified.example"
    }).Build();

async Task CheckFailure(HttpStatusCode status, string response, string expectedCode, string? key = " test-api-key ")
{
    using var handler = new FakeHandler(status, response);
    using var client = new HttpClient(handler);
    var service = new EmailService(Configuration(key), client);
    try
    {
        await service.SendOtpAsync("employee@example.test", "123456");
        throw new Exception($"Expected {expectedCode}, but delivery succeeded.");
    }
    catch (EmailDeliveryException error)
    {
        if (error.Code != expectedCode) throw new Exception($"Expected {expectedCode}, got {error.Code}");
        if (error.Message.Contains("owner@example.test") || error.Message.Contains("test-api-key"))
            throw new Exception("Provider details leaked into client message.");
        checks++;
    }
    if (string.IsNullOrWhiteSpace(key) && handler.Calls != 0)
        throw new Exception("Missing credentials must not cause an HTTP request.");
}

await CheckFailure(HttpStatusCode.OK, "{}", "EMAIL_NOT_CONFIGURED", null);
await CheckFailure(HttpStatusCode.OK, "{}", "EMAIL_NOT_CONFIGURED", " ");
await CheckFailure(HttpStatusCode.Forbidden,
    """{"message":"You can only send testing emails to your own email address (owner@example.test)."}""",
    "EMAIL_TEST_MODE");
await CheckFailure(HttpStatusCode.Forbidden,
    """{"message":"The verified.example domain is not verified."}""", "EMAIL_DOMAIN_NOT_VERIFIED");
await CheckFailure(HttpStatusCode.Unauthorized, "{}", "EMAIL_PROVIDER_AUTH_FAILED");
await CheckFailure(HttpStatusCode.Forbidden, "{}", "EMAIL_PROVIDER_AUTH_FAILED");
await CheckFailure(HttpStatusCode.TooManyRequests, "{}", "EMAIL_RATE_LIMITED");
await CheckFailure(HttpStatusCode.ServiceUnavailable, "<html>Unavailable</html>", "EMAIL_SERVICE_UNAVAILABLE");
await CheckFailure(HttpStatusCode.UnprocessableEntity, "{}", "EMAIL_SERVICE_UNAVAILABLE");

using (var handler = new FakeHandler(HttpStatusCode.OK, """{"id":"test-email"}"""))
using (var client = new HttpClient(handler))
{
    var service = new EmailService(Configuration(" test-api-key "), client);
    await service.SendOtpAsync("employee@example.test", "123456");
    using var payload = JsonDocument.Parse(handler.Body!);
    if (handler.Authorization != "Bearer test-api-key"
        || payload.RootElement.GetProperty("to")[0].GetString() != "employee@example.test"
        || !payload.RootElement.GetProperty("html").GetString()!.Contains("123456"))
        throw new Exception("Email request was not constructed correctly.");
    checks++;
}

Console.WriteLine($"PASS: {checks} email delivery checks; no real emails sent.");

sealed class FakeHandler(HttpStatusCode status, string response) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? Body { get; private set; }
    public string? Authorization { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        if (request.RequestUri?.AbsoluteUri != "https://api.resend.com/emails" || request.Method != HttpMethod.Post)
            throw new Exception("Unexpected email endpoint.");
        Authorization = request.Headers.Authorization?.ToString();
        Body = await request.Content!.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(response) };
    }
}
