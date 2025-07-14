namespace Toletus.LiteNet3.Handler.Responses.FetchesResponse;

public class FlowResponse
{
    public bool Inverted { get; set; }
    public string? In { get; set; }
    public string? Out { get; set; }
    public string? FrontWait { get; set; }
    public int PictoWaitIn { get; set; }
    public int PictoWaitOut { get; set; }
}