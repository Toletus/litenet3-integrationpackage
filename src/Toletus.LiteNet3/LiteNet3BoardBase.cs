using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Toletus.LiteNet3.Handler;
using Toletus.LiteNet3.Handler.Requests;
using Toletus.LiteNet3.Handler.Responses.Base;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses.Base;
using Toletus.LiteNet3.Server;
using Toletus.Pack.Core;
using WebSocketSharp;

namespace Toletus.LiteNet3;

public class LiteNet3BoardBase
{
    private readonly ResponseDispatcher _dispatcher = new();
    private LiteNet3WebSocketBehavior? _currentBehavior;
    private readonly int _portServer;

    private static readonly Dictionary<string, LiteNet3WebSocket> LiteNet3WebSockets = new();
    private static readonly Dictionary<string, WebSocket> WebSockets = new();
    private string ConnectionInfo { get; set; }

    public IPAddress Ip { get; set; }
    public IPAddress? NetworkIp { get; set; }
    public const int Port = 7878;
    public int Id { get; set; }
    public string? Serial { get; set; }
    public string? Alias { get; set; }
    public string? ServerUri { get; set; }
    public string? Firmware { get; set; }
    public string? Hardware { get; set; }

    public bool InUse => Connected
                         || ConnectionInfo.Length > 0 && ConnectionInfo != "Disconnected";

    public bool HasFingerprintReader { get; set; }
    public bool Connected { get; private set; }
    public object? Tag { get; set; }
    public string? Mac { get; set; }
    public string? Description { get; set; }
    public IpConfig? IpConfig { get; set; }
    public string? MenuPassword { get; set; }
    public int? ReleaseDuration { get; set; }
    public bool? ShowCounters { get; set; }
    public bool? EntryClockwise { get; set; }
    public bool? BuzzerMute { get; set; }

    public Action<LiteNet3BoardBase, CodeBase>? OnIdentification;
    public Action<LiteNet3BoardBase, object>? OnResponse;
    public Action<LiteNet3BoardBase, ResultBase>? OnResult;
    public Action<LiteNet3BoardBase, byte[]>? OnBiometricsResponse;
    public Action<LiteNet3BoardBase, ReleaseBase>? OnReleaseResponse;

    public LiteNet3BoardBase(
        IPAddress ip,
        bool connected,
        int? id = null,
        string connectionInfo = "",
        string? serial = null,
        string? alias = null)
    {
        Connected = connected;
        Ip = ip;
        if (id.HasValue) Id = id.Value;
        ConnectionInfo = connectionInfo == "None" ? "Disconnected" : connectionInfo;
        _portServer = GetAvailablePort();
        Serial = serial;
        Alias = alias;
    }

    public override string ToString() => $"LiteNet3 #{Id} {Ip}:{Port} {ConnectionInfo}";

    public void Connect(string network)
    {
        WebSockets.TryGetValue(Serial, out var webSocket);
        if (webSocket is { IsAlive: true })
            return;
        
        NetworkIp = NetworkHelper.GetLocalNetworkAddress(network);
        var serverUri = $"ws://{NetworkIp}:{_portServer}";

        if (!serverUri.Equals(ServerUri, StringComparison.OrdinalIgnoreCase))
            LiteNetUtil.SetServer(Ip, serverUri, Serial);

        var uri = $"http://{NetworkIp}:{_portServer}/";

        var liteNet3WebSocket = new LiteNet3WebSocket
        {
            OnNewBehavior = (behavior) =>
            {
                if (_currentBehavior != null)
                {
                    _currentBehavior.ConnectedEvent -= OnConnected;
                    _currentBehavior.DisconnectedEvent -= OnDisconnected;
                    _currentBehavior.MessageEvent -= OnMessage;
                }

                _currentBehavior = behavior;
                behavior.ConnectedEvent += OnConnected;
                behavior.DisconnectedEvent += OnDisconnected;
                behavior.MessageEvent += OnMessage;
            }
        };

        liteNet3WebSocket.StartAsync(uri);
        LiteNet3WebSockets.TryAdd(Serial, liteNet3WebSocket);

        Console.WriteLine($"StartAsync: {DateTime.Now}");

        while (!Connected)
        {
        }
    }

    public void Close()
    {
        DisconnectWebSocketClients(Serial);
        Connected = false;
    }

    protected void Send(RequestBase request)
    {
        var json = request.Serialize();

        if (WebSockets.TryGetValue(Serial, out var value))
            value.Send(json);
    }

    private static void DisconnectWebSocketClients(string serial, bool isOnDisconnect = false)
    {
        if (!LiteNet3WebSockets.TryGetValue(serial, out var liteNet3WebSocket)) return;

        if (!isOnDisconnect)
            liteNet3WebSocket.StopAndClearWebSocketServer();
        
        LiteNet3WebSockets.Remove(serial);
        WebSockets.Remove(serial);
    }


    private void ToggleWebSocketEventSubscriptions(bool isRegister = true)
    {
        if (isRegister)
        {
            _dispatcher.OnNotificationResponse += OnNotificationResponse;
            _dispatcher.OnNotificationBiometricsResponse += OnNotificationBiometricsResponse;
            _dispatcher.OnFetchResponse += OnOnFetchResponseHandler;
            _dispatcher.OnUpdateResponse += OnChangeResponseHandler;
            _dispatcher.OnActionResponse += OnChangeResponseHandler;
        }
        else
        {
            _dispatcher.OnNotificationResponse -= OnNotificationResponse;
            _dispatcher.OnNotificationBiometricsResponse -= OnNotificationBiometricsResponse;
            _dispatcher.OnFetchResponse -= OnOnFetchResponseHandler;
            _dispatcher.OnUpdateResponse -= OnChangeResponseHandler;
            _dispatcher.OnActionResponse -= OnChangeResponseHandler;
        }
    }

    private void OnChangeResponseHandler(ResultBase? obj)
    {
        if (obj == null) return;

        OnResult?.Invoke(this, obj);
    }

    private void OnOnFetchResponseHandler(object? obj)
    {
        if (obj == null) return;

        OnResponse?.Invoke(this, obj);
    }

    private void OnNotificationResponse(object? obj)
    {
        switch (obj)
        {
            case null:
                return;
            case PingResponse:
                Connected = true;
                OnResponse?.Invoke(this, obj);
                return;
            case CodeBase code:
                OnIdentification?.Invoke(this, code);
                return;
            case PassageResponse response:
                OnReleaseResponse?.Invoke(this, response);
                Console.WriteLine(JsonSerializer.Serialize(obj));
                return;
            case TimeoutResponse response:
                OnReleaseResponse?.Invoke(this, response);
                Console.WriteLine(JsonSerializer.Serialize(obj));
                return;
            default:
                Console.WriteLine(JsonSerializer.Serialize(obj));
                break;
        }
    }

    private void OnNotificationBiometricsResponse(byte[] obj)
    {
        OnBiometricsResponse?.Invoke(this, obj);
    }

    private void OnConnected(WebSocket obj, string serial)
    {
        Connected = true;
        WebSockets.TryAdd(serial, obj);
        ToggleWebSocketEventSubscriptions();
        Console.WriteLine($"OnConnected: {Connected} {DateTime.Now}");
    }

    private void OnDisconnected(string serial)
    {
        Console.WriteLine($"OnDisconnected: {Connected} {DateTime.Now}");
        DisconnectWebSocketClients(serial, true);
        ToggleWebSocketEventSubscriptions(isRegister: false);
        Connected = false;
    }

    private void OnMessage(string obj)
    {
        _dispatcher.Dispatch(obj);
    }

    private static int GetAvailablePort()
    {
        const int minPort = 1024;
        const int maxPort = 65535;
        var random = new Random();

        while (true)
        {
            var port = random.Next(minPort, maxPort);

            if (IsPortAvailable(port))
                return port;
        }
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}