using Toletus.LiteNet3.Handler.Responses.NotificationsResponses.Base;

namespace Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

public class TimeoutResponse : ReleaseBase
{
    public string Release { get; set; }
}