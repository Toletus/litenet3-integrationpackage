using Newtonsoft.Json.Linq;

namespace Toletus.LiteNet3.Handler.Responses.Base;

public abstract class ResponseBase
{
    public JObject Data { get; set; }
    public virtual TResponse? GetData<TResponse>() where TResponse : class
    {
        return Data.ToObject<TResponse>();
    }
}