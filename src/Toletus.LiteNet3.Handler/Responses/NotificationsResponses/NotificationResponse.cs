using Toletus.LiteNet3.Handler.Responses.Base;

namespace Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

public class NotificationResponse : ResponseBase
{
    public event Action<object?>? OnNotificationResponse;
    public event Action<byte[]>? OnNotificationBiometricsResponse;
    public ResponseType Notification { get; set; }

    public void Proccess()
    {
        switch (Notification)
        {
            case ResponseType.Ping:
                OnNotificationResponse?.Invoke(GetData<PingResponse>());
                break;
            case ResponseType.Rfid:
                OnNotificationResponse?.Invoke(GetData<RfidResponse>());
                break;
            case ResponseType.Barcode:
                OnNotificationResponse?.Invoke(GetData<BarcodeResponse>());
                break;
            case ResponseType.Keypad:
                OnNotificationResponse?.Invoke(GetData<KeypadResponse>());
                break;
            case ResponseType.Passage:
                OnNotificationResponse?.Invoke(GetData<PassageResponse>());
                break;
            case ResponseType.Timeout:
                OnNotificationResponse?.Invoke(GetData<TimeoutResponse>());
                break;
            case ResponseType.Biometrics:
                var biometrics = GetData<BiometricsResponse>();
                if (biometrics != null && biometrics.Process())
                {
                    var decompressedImage = biometrics.GetImage().DecompressedImage;
                    OnNotificationBiometricsResponse?.Invoke(decompressedImage);
                }
                break;
        }
    }
}