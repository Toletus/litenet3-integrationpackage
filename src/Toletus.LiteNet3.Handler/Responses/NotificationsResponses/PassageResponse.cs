using Toletus.LiteNet3.Handler.Responses.NotificationsResponses.Base;

namespace Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

public class PassageResponse : ReleaseBase
{
    public int? In { get; set; }
    public int? Out { get; set; }
}