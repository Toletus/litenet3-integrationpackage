using Toletus.LiteNet3.Handler.Responses.Base;

namespace Toletus.LiteNet3.Handler.Responses.UpdatesResponse;

public class UpdateResponse : ResponseBase
{
    public ResponseType Update { get; set; }
    public event Action<ResultBase?>? OnUpdateResponseHandler;

    public void Proccess()
    {
        switch (Update)
        {
            case ResponseType.Display:
                OnUpdateResponseHandler?.Invoke(GetData<DisplayUpdateResponse>());
                break;
            case ResponseType.Discovery:
                OnUpdateResponseHandler?.Invoke(GetData<DiscoveryUpdateResponse>());
                break;
            case ResponseType.Ethernet:
                OnUpdateResponseHandler?.Invoke(GetData<EthernetUpdateResponse>());
                break;
            case ResponseType.Buzzer:
                OnUpdateResponseHandler?.Invoke(GetData<BuzzerUpdateResponse>());
                break;
            case ResponseType.Flow:
                OnUpdateResponseHandler?.Invoke(GetData<FlowUpdateResponse>());
                break;
            case ResponseType.LiteNet3:
                OnUpdateResponseHandler?.Invoke(GetData<LiteNet3UpdateResponse>());
                break;
            case ResponseType.Sensor:
                OnUpdateResponseHandler?.Invoke(GetData<SensorUpdateResponse>());
                break;
            case ResponseType.Server:
                OnUpdateResponseHandler?.Invoke(GetData<ServerUpdateResponse>());
                break;
            case ResponseType.Factory:
                OnUpdateResponseHandler?.Invoke(GetData<FactoryUpdateResponse>());
                break;
        }
    }
}

public class DisplayUpdateResponse : ResultBase;
public class DiscoveryUpdateResponse : ResultBase;
public class EthernetUpdateResponse : ResultBase;
public class BuzzerUpdateResponse : ResultBase;
public class FlowUpdateResponse : ResultBase;
public class LiteNet3UpdateResponse : ResultBase;
public class SensorUpdateResponse : ResultBase;
public class ServerUpdateResponse : ResultBase;
public class FactoryUpdateResponse : ResultBase;