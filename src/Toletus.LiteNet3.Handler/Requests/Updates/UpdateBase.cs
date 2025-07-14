using System.Text.Json;
using System.Text.Json.Serialization;

namespace Toletus.LiteNet3.Handler.Requests.Updates;

public class UpdateBase : RequestBase
{
    [JsonPropertyName("update")]
    public string? Update { get; set; }
    public override string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}