using Newtonsoft.Json.Linq;

namespace Toletus.LiteNet3.Handler.Responses.Base;

public abstract class ResponseBase
{
    public JObject? Data { get; set; }
    public string? Result { get; set; }
    public string? Reason { get; set; }
    public string? Key { get; set; }

    public virtual TResponse? GetData<TResponse>() where TResponse : class
    {
        if (Data != null)
            return Data.ToObject<TResponse>();

        if (!typeof(ResultBase).IsAssignableFrom(typeof(TResponse)))
            return null;

        return JObject.FromObject(new
        {
            result = Result,
            reason = Reason,
            key = Key,
        }).ToObject<TResponse>();
    }
}