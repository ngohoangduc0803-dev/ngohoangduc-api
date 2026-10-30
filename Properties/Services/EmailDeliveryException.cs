namespace ngohoangduc.Services;

// Only controlled, non-sensitive messages from this exception are sent to clients.
public sealed class EmailDeliveryException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
