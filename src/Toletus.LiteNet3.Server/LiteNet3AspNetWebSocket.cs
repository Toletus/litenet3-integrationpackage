using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

#nullable enable

namespace Toletus.LiteNet3.Server;

/// <summary>
/// ASP.NET Core implementation of the LiteNet3 WebSocket server. The public surface mimics the
/// WebSocketSharp-based counterpart so the rest of the solution can swap implementations later.
/// </summary>
public class LiteNet3AspNetWebSocket
{
    private static readonly ConcurrentDictionary<string, bool> ActiveConnections = new();
    private readonly object _lifetimeLock = new();
    private WebApplication? _app;
    private CancellationTokenSource? _applicationCts;
    private Task? _runTask;

    public string Log { get; internal set; } = string.Empty;

    public Action<LiteNet3AspNetWebSocketConnection>? OnNewConnection;

    public void StartAsync(string uri)
    {
        lock (_lifetimeLock)
        {
            StopInternal();

            try
            {
                var uriObject = new Uri(uri);
                var builder = WebApplication.CreateSlimBuilder();

                builder.WebHost.ConfigureKestrel(options =>
                {
                    if (IPAddress.TryParse(uriObject.Host, out var ipAddress))
                    {
                        options.Listen(ipAddress, uriObject.Port);
                    }
                    else
                    {
                        options.ListenAnyIP(uriObject.Port);
                    }
                });

                var app = builder.Build();

                var webSocketOptions = new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(30)
                };

                app.UseWebSockets(webSocketOptions);
                app.Map("/", RequestDelegate);

                _applicationCts = new CancellationTokenSource();
                _app = app;
                _runTask = app.RunAsync(_applicationCts.Token);

                Log = $"ASP.NET Core WebSocket server started at {uri}";
                Console.WriteLine(Log);
            }
            catch (Exception ex)
            {
                Log = $"Failed to start ASP.NET Core WebSocket server: {ex.Message}";
                Console.WriteLine(Log);
            }
        }
    }

    private void RequestDelegate(IApplicationBuilder appBuilder)
    {
        appBuilder.Run(async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Expected WebSocket request", context.RequestAborted);
                return;
            }

            var connection = await LiteNet3AspNetWebSocketConnection.CreateAsync(
                this,
                context,
                _applicationCts?.Token ?? CancellationToken.None);

            if (connection == null)
                return;

            OnNewConnection?.Invoke(connection);
            await connection.ProcessAsync(_applicationCts?.Token ?? CancellationToken.None);
        });
    }

    public void StopAndClearWebSocketServer()
    {
        lock (_lifetimeLock)
        {
            StopInternal();
        }
    }

    private void StopInternal()
    {
        try
        {
            if (_app == null)
            {
                ActiveConnections.Clear();
                return;
            }

            Console.WriteLine("Stopping ASP.NET Core WebSocket server...");
            _applicationCts?.Cancel();

            _app.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _runTask?.GetAwaiter().GetResult();

            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _app = null;

            _applicationCts?.Dispose();
            _applicationCts = null;
            _runTask = null;

            ActiveConnections.Clear();
            Console.WriteLine("ASP.NET Core WebSocket server stopped and resources cleared.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while stopping ASP.NET Core WebSocket server: {ex.Message}");
        }
    }

    internal void RegisterConnection(string serial)
    {
        ActiveConnections.TryAdd(serial, true);
        Console.WriteLine($"ASP.NET Core client {serial} registered. Total connections: {ActiveConnections.Count}");
    }

    internal void UnregisterConnection(string serial)
    {
        ActiveConnections.TryRemove(serial, out _);
        Console.WriteLine($"ASP.NET Core client {serial} unregistered. Remaining connections: {ActiveConnections.Count}");

        if (!ActiveConnections.IsEmpty) return;

        Console.WriteLine("No active ASP.NET Core WebSocket connections. Stopping server.");
        StopAndClearWebSocketServer();
    }
}
