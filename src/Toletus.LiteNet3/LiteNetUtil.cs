using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Toletus.LiteNet3.Handler;
using Toletus.LiteNet3.Handler.Requests.Fetches;
using Toletus.LiteNet3.Handler.Requests.Updates;
using Toletus.LiteNet3.Handler.Responses.FetchesResponse;
using Toletus.Pack.Core.Network.Utils;
using Toletus.Pack.Core.Utils;

namespace Toletus.LiteNet3;

public static class LiteNetUtil
{
    private static readonly List<LiteNet3BoardBase> DiscoveredBoards = [];
    private const int DefaultDiscoveryPort = 7878;

    static LiteNetUtil()
    {
        UdpUtils.OnUdpResponse += HandleUdpResponse;
    }

    public static List<LiteNet3BoardBase> Search(IPAddress networkIpAddress)
    {
        DiscoveredBoards.Clear();

        var discoveryRequest = new DiscoveryFetch();
        var requestContent = JsonSerializer.Serialize(discoveryRequest);

        UdpUtils.Send(networkIpAddress, DefaultDiscoveryPort, requestContent);

        foreach (var board in DiscoveredBoards)
            board.NetworkIp = networkIpAddress;

        return DiscoveredBoards;
    }

    public static void SetServer(IPAddress ip, string serverUri, string serial)
    {
        var serverSet = new ServerUpdate(serial, serverUri);
        var requestContent = JsonSerializer.Serialize(serverSet);
        var udpClient = new UdpClient();
        var data = Encoding.ASCII.GetBytes(requestContent);
        udpClient.Send(data, data.Length, ip.ToString(), DefaultDiscoveryPort);
    }

    public static LiteNet3BoardBase? Search(string networkInterfaceName, int? id)
    {
        var boards = Search(networkInterfaceName);
        return boards?.FirstOrDefault(b => !id.HasValue || b.Id == id.Value);
    }

    private static List<LiteNet3BoardBase>? Search(string networkInterfaceName)
    {
        var ipAddress = NetworkInterfaceUtils.GetNetworkInterfaceIpAddressByName(networkInterfaceName);
        return ipAddress == null ? null : Search(ipAddress);
    }

    private static void HandleUdpResponse(UdpClient udpClient, Task<UdpReceiveResult> responseTask)
    {
        if (!responseTask.IsCompletedSuccessfully)
            return;

        var response = responseTask.Result;
        var receivedMessage = Encoding.ASCII.GetString(response.Buffer);

        var discoveryResponse = ParseDiscoveryResponse(receivedMessage);
        if (discoveryResponse == null)
            return;

        var discoveredBoard = new LiteNet3BoardBase(
            response.RemoteEndPoint.Address,
            discoveryResponse.Connected,
            discoveryResponse.Id,
            connectionInfo: string.Empty,
            discoveryResponse.Serial,
            discoveryResponse.Alias);

        DiscoveredBoards.Add(discoveredBoard);
    }

    private static DiscoveryResponse? ParseDiscoveryResponse(string message)
    {
        var jsonObject = JObjectUtil.Parse(message);
        if (jsonObject == null || !jsonObject.ContainsKey(nameof(FetchResponse.Fetch).ToLower()))
            return null;

        var getResponse = jsonObject.ToObject<FetchResponse>();
        return getResponse?.GetData<DiscoveryResponse>();
    }
}