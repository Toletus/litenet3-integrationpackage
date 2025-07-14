using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Toletus.LiteNet3.Server;

public static class NetworkHelper
{
    private const string VirtualAdapterDescription = "Virtual";

    public static IPAddress? GetLocalNetworkAddress(string? specificNetworkName = null)
    {
        var validNetworkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(ni => IsValidNetworkInterface(ni, specificNetworkName));

        if (validNetworkInterface == null)
            return null;

        var unicastAddresses = validNetworkInterface.GetIPProperties().UnicastAddresses;

        return GetIPv4Address(unicastAddresses);
    }

    private static bool IsValidNetworkInterface(NetworkInterface networkInterface, string? specificNetworkName)
    {
        var isBaseValid = !networkInterface.Description.Contains(VirtualAdapterDescription) &&
                          (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                           networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                          networkInterface.OperationalStatus == OperationalStatus.Up &&
                          networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback;

        // Opcional: verificar correspondência com o nome da rede específica
        var matchesNetworkName = string.IsNullOrEmpty(specificNetworkName) ||
                                 networkInterface.Name.Contains(specificNetworkName, StringComparison.OrdinalIgnoreCase);

        return isBaseValid && matchesNetworkName;
    }

    private static IPAddress? GetIPv4Address(UnicastIPAddressInformationCollection addresses)
    {
        return addresses
            .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            ?.Address;
    }
}