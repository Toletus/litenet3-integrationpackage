namespace Toletus.LiteNet3.Handler.Responses.FetchesResponse;

public class EthernetResponse
{
    public string? Ip { get; set; }
    public string? Mask { get; set; }
    public string? Gateway { get; set; }
    public bool StaticIp { get; set; }
    public int[] Mac { get; set; } = new int[6];
}