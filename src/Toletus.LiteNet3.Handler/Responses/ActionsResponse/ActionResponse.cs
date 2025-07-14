using Toletus.LiteNet3.Handler.Responses.Base;

namespace Toletus.LiteNet3.Handler.Responses.ActionsResponse;

public class ActionResponse : ResponseBase
{
    public ResponseType Action { get; set; }
    public event Action<ResultBase?>? OnActionResponseHandler;

    public void Proccess()
    {
        switch (Action)
        {
            case ResponseType.Display:
                OnActionResponseHandler?.Invoke(GetData<DisplayActionResponse>());
                break;
            case ResponseType.Buzzer:
                OnActionResponseHandler?.Invoke(GetData<BuzzerActionResponse>());
                break;
            case ResponseType.LiteNet3:
                OnActionResponseHandler?.Invoke(GetData<LiteNet3ActionResponse>());
                break;
        }
    }
}

public class DisplayActionResponse : ResultBase;
public class BuzzerActionResponse : ResultBase;
public class LiteNet3ActionResponse : ResultBase;
