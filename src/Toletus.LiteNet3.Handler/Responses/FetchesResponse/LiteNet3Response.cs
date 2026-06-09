using Newtonsoft.Json;

namespace Toletus.LiteNet3.Handler.Responses.FetchesResponse;

public class LiteNet3Response
{
    public string? Serial { get; set; }
    public int ReleaseTime { get; set; }
    public string? Alias { get; set; }
    public int Id { get; set; }
    public string? MenuPass { get; set; }
    public List<string>? Supported { get; set; }

    [JsonProperty("disable")]
    public List<string>? Disable { get; set; }

    [JsonProperty("error")]
    public List<string>? Error { get; set; }

    [JsonIgnore]
    public List<string>? Installed
    {
        get => Disable;
        set => Disable = value;
    }

    [JsonIgnore]
    public List<string>? DevicesError
    {
        get => Error;
        set => Error = value;
    }

    public List<string>? GeneralErros { get; set; }
}