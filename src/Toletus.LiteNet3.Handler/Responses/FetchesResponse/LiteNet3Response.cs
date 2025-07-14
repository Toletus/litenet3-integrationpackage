namespace Toletus.LiteNet3.Handler.Responses.FetchesResponse;

public class LiteNet3Response
{
    public string? Serial { get; set; }
    public int ReleaseTime { get; set; }
    public string? Alias { get; set; }
    public int Id { get; set; }
    public string? MenuPass { get; set; }
    public List<string>? Supported { get; set; }
    public List<string>? Installed { get; set; }
    public List<string>? DevicesError { get; set; }
    public string? GeneralErros { get; set; }
}