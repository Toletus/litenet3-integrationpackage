using Toletus.LiteNet3.Handler.Responses.NotificationsResponses.Base;

namespace Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

public class ErrorResponse : SerialBase
{
    public string Device { get; set; } = string.Empty;
}