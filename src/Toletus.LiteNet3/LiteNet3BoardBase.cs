using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Toletus.LiteNet3.Handler;
using Toletus.LiteNet3.Handler.Requests;
using Toletus.LiteNet3.Handler.Requests.Fetches;
using Toletus.LiteNet3.Handler.Responses.Base;
using Toletus.LiteNet3.Handler.Responses.FetchesResponse;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses.Base;
using Toletus.LiteNet3.SerialPort;
using Toletus.LiteNet3.Server;
using Toletus.Pack.Core.Network;
using WebSocketSharp;

namespace Toletus.LiteNet3;

public class LiteNet3BoardBase
{
    private readonly ManualResetEventSlim _connectedEvent = new(initialState: false);

    private readonly ResponseDispatcher _dispatcher = new();
    private readonly Lock _subscriptionLock = new();
    private bool _dispatcherSubscribed;
    private LiteNet3WebSocketBehavior? _currentBehavior;
    private readonly int _portServer;
    private readonly ConcurrentDictionary<string, Guid> _activeConnectionIds = new();
    private static readonly ConcurrentDictionary<string, object> ConnectLocks = new();

    private static readonly Dictionary<string, LiteNet3WebSocket> LiteNet3WebSockets = new();
    private static readonly Dictionary<string, WebSocket> WebSockets = new();
    private static SerialService? _serialService;

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

    protected LiteNet3BoardBase(string connectionInfo = "")
    {
        ConnectionInfo = connectionInfo == "None" ? "Disconnected" : connectionInfo;
    }

    public override string ToString() => $"LiteNet3 #{Id} {Ip}:{Port} {ConnectionInfo}";

    public void Connect(string network)
    {
        _connectedEvent.Reset();

        WebSockets.TryGetValue(Serial, out var webSocket);
        if (webSocket is { IsAlive: true })
            return;

        NetworkIp = NetworkHelper.GetLocalNetworkAddress(network);
        var serverUri = $"ws://{NetworkIp}:{_portServer}";

        if (!serverUri.Equals(ServerUri, StringComparison.OrdinalIgnoreCase))
            LiteNetUtil.SetServer(Ip, serverUri, Serial);

        var uri = $"http://{NetworkIp}:{_portServer}/";

        var lockObj = ConnectLocks.GetOrAdd(Serial, _ => new object());
        lock (lockObj)
        {
            if (!LiteNet3WebSockets.TryGetValue(Serial, out var existingServer))
            {
                existingServer = new LiteNet3WebSocket();
                LiteNet3WebSockets[Serial] = existingServer;
                existingServer.OnNewBehavior = (behavior) =>
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
                };

                existingServer.Start(uri);
            }
            else
            {
                existingServer.OnNewBehavior = (behavior) =>
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
                };
            }
        }

        Console.WriteLine($"Start: {DateTime.Now}");

        if (!_connectedEvent.Wait(TimeSpan.FromSeconds(30)))
            throw new TimeoutException("LiteNet3 failed to connect within 30 seconds.");
    }

    public void ConnectSerialPort(string? serialPortName = null)
    {
        if (_serialService?.IsOpen == true)
            Close();

        _serialService = new SerialService();
        _serialService.Start(serialPortName);
        _serialService.MessageEvent += OnMessage;
        Connected = true;

        var liteNet3Response = _serialService.SendAndWaitResponse<LiteNet3Response>(new LiteNet3Fetch().Serialize());

        Id = liteNet3Response.Id;
        Alias = liteNet3Response.Alias;
        Serial = liteNet3Response.Serial;
        Ip = IPAddress.Loopback;

        ToggleWebSocketEventSubscriptions();
    }

    public void Close()
    {
        if (_serialService != null)
            CloseSerialPort();
        else
            DisconnectWebSocketClients(Serial);

        Connected = false;
    }

    protected void Send(RequestBase request)
    {
        var json = request.Serialize();

        if (_serialService?.IsOpen == true)
        {
            Console.WriteLine($"[LiteNet3] Send via serial: {json}");
            _serialService.Send(json);
            return;
        }

        if (!WebSockets.TryGetValue(Serial, out var value))
        {
            Console.WriteLine($"[LiteNet3] Send aborted: no WebSocket for serial {Serial}.");
            return;
        }

        if (value is not { IsAlive: true })
        {
            Console.WriteLine($"[LiteNet3] Send aborted: WebSocket not alive for serial {Serial}.");
            return;
        }

        try
        {
            value.Send(json);
        }
        catch (IOException ioEx)
        {
            Console.WriteLine($"[LiteNet3] Send failed (IO) for serial {Serial}: {ioEx.Message}");
        }
        catch (SocketException sockEx)
        {
            Console.WriteLine($"[LiteNet3] Send failed (Socket) for serial {Serial}: {sockEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LiteNet3] Send failed (Unexpected) for serial {Serial}: {ex.Message}");
        }
    }

    private void CloseSerialPort()
    {
        _serialService?.Stop();
        _serialService = null;

        ToggleWebSocketEventSubscriptions(isRegister: false);
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
        lock (_subscriptionLock)
        {
            if (isRegister)
            {
                if (_dispatcherSubscribed)
                    return;

                _dispatcher.OnNotificationResponse += OnNotificationResponse;
                _dispatcher.OnNotificationBiometricsResponse += OnNotificationBiometricsResponse;
                _dispatcher.OnFetchResponse += OnOnFetchResponseHandler;
                _dispatcher.OnUpdateResponse += OnChangeResponseHandler;
                _dispatcher.OnActionResponse += OnChangeResponseHandler;

                _dispatcherSubscribed = true;
                Console.WriteLine("[LiteNet3] Dispatcher handlers subscribed.");
            }
            else
            {
                if (!_dispatcherSubscribed)
                    return;

                _dispatcher.OnNotificationResponse -= OnNotificationResponse;
                _dispatcher.OnNotificationBiometricsResponse -= OnNotificationBiometricsResponse;
                _dispatcher.OnFetchResponse -= OnOnFetchResponseHandler;
                _dispatcher.OnUpdateResponse -= OnChangeResponseHandler;
                _dispatcher.OnActionResponse -= OnChangeResponseHandler;

                _dispatcherSubscribed = false;
                Console.WriteLine("[LiteNet3] Dispatcher handlers unsubscribed.");
            }
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

    private void OnConnected(WebSocket obj, string serial, Guid connectionId)
    {
        Connected = true;
        _activeConnectionIds[serial] = connectionId;
        if (WebSockets.TryGetValue(serial, out var oldWs))
        {
            if (!ReferenceEquals(oldWs, obj))
            {
                try
                {
                    if (oldWs.IsAlive)
                        oldWs.Close(CloseStatusCode.Normal, "Replaced by new connection");
                }
                catch
                {
                    /* ignore */
                }

                Console.WriteLine($"[LiteNet3] Replacing existing WebSocket for serial {serial}.");
            }
        }

        WebSockets[serial] = obj;
        ToggleWebSocketEventSubscriptions();
        Console.WriteLine($"OnConnected: {Connected} {DateTime.Now}");
        _connectedEvent.Set();
    }

    private void OnDisconnected(string serial, Guid connectionId)
    {
        Console.WriteLine($"OnDisconnected: {Connected} {DateTime.Now}");
        if (_activeConnectionIds.TryGetValue(serial, out var current) && current != connectionId)
        {
            Console.WriteLine(
                $"[LiteNet3] Ignoring OnDisconnected from stale connection {connectionId} for serial {serial}.");
            return;
        }

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