namespace Toletus.LiteNet3.Handler.Responses.FetchesResponse;

public class DiscoveryResponse
{
    public string? Serial { get; set; }
    public int Id { get; set; }
    public string? Alias { get; set; }
    public string? ServerUri { get; set; }
    public string? Ip { get; set; }
    public bool Connected { get; set; }
    public string? Firmware { get; set; }
    public string? Hardware { get; set; }
}