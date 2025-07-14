using Toletus.LiteNet3.Handler.Responses.ActionsResponse;
using Toletus.LiteNet3.Handler.Responses.Base;
using Toletus.LiteNet3.Handler.Responses.FetchesResponse;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;
using Toletus.LiteNet3.Handler.Responses.UpdatesResponse;

namespace Toletus.LiteNet3.Handler;

public class ResponseDispatcher
{
    public event Action<object?>? OnNotificationResponse;
    public event Action<byte[]>? OnNotificationBiometricsResponse;
    public event Action<object?>? OnFetchResponse;
    public event Action<ResultBase?>? OnUpdateResponse;
    public event Action<ResultBase?>? OnActionResponse;

    public void Dispatch(string json)
    {
        var jsonObject = JObjectUtil.Parse(json);

        var key = jsonObject?.Properties().FirstOrDefault()?.Name;

        if (key == null)
            return;

        switch (key)
        {
            case "notification":
            {
                var response = jsonObject?.ToObject<NotificationResponse>();
                if (response == null) return;

                response.OnNotificationResponse += (obj) => OnNotificationResponse?.Invoke(obj);
                response.OnNotificationBiometricsResponse += (data) => OnNotificationBiometricsResponse?.Invoke(data);
                response.Proccess();
                break;
            }

            case "fetch":
            {
                var response = jsonObject?.ToObject<FetchResponse>();
                if (response == null) return;

                response.OnFetchResponseHandler += (obj) => OnFetchResponse?.Invoke(obj);
                response.Proccess();
                break;
            }

            case "update":
            {
                var response = jsonObject?.ToObject<UpdateResponse>();
                if (response == null) return;

                response.OnUpdateResponseHandler += (result) => OnUpdateResponse?.Invoke(result);
                response.Proccess();
                break;
            }

            case "action":
            {
                var response = jsonObject?.ToObject<ActionResponse>();
                if (response == null) return;

                response.OnActionResponseHandler += (result) => OnActionResponse?.Invoke(result);
                response.Proccess();
                break;
            }

            default:
                Console.WriteLine(json);
                break;
        }
    }
}