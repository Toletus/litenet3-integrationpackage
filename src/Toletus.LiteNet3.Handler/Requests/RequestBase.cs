using System.Text.Json.Serialization;

namespace Toletus.LiteNet3.Handler.Requests;

public abstract class RequestBase
{
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    public abstract string Serialize();
}