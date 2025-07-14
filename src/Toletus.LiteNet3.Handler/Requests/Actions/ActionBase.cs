using System.Text.Json;
using System.Text.Json.Serialization;

namespace Toletus.LiteNet3.Handler.Requests.Actions;

public abstract class ActionBase : RequestBase
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }
    
    public override string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}