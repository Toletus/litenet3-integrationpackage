using Toletus.LiteNet3.Handler.Responses.Base;

namespace Toletus.LiteNet3.Handler.Responses.FetchesResponse;

public class FetchResponse : ResponseBase
{
    public ResponseType Fetch { get; set; }
    
    public event Action<object?>? OnFetchResponseHandler;

    public void Proccess()
    {
        switch (Fetch)
        {
            case ResponseType.Display:
                OnFetchResponseHandler?.Invoke(GetData<DisplayResponse>());
                break;
            case ResponseType.Discovery:
                OnFetchResponseHandler?.Invoke(GetData<DiscoveryResponse>());
                break;
            case ResponseType.Ethernet:
                OnFetchResponseHandler?.Invoke(GetData<EthernetResponse>());
                break;
            case ResponseType.Buzzer:
                OnFetchResponseHandler?.Invoke(GetData<BuzzerResponse>());
                break;
            case ResponseType.Flow:
                OnFetchResponseHandler?.Invoke(GetData<FlowResponse>());
                break;
            case ResponseType.LiteNet3:
                OnFetchResponseHandler?.Invoke(GetData<LiteNet3Response>());
                break;
            case ResponseType.Sensor:
                OnFetchResponseHandler?.Invoke(GetData<SensorResponse>());
                break;
            case ResponseType.Server:
                OnFetchResponseHandler?.Invoke(GetData<ServerResponse>());
                break;
            case ResponseType.Factory:
                OnFetchResponseHandler?.Invoke(GetData<FactoryResponse>());
                break;
        }
    }
}