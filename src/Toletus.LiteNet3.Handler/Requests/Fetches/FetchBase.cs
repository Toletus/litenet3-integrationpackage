using System.Text.Json;
using System.Text.Json.Serialization;

namespace Toletus.LiteNet3.Handler.Requests.Fetches;

public class FetchBase : RequestBase
{
    [JsonPropertyName("fetch")]
    public string? Fetch { get; set; }
    public override string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}