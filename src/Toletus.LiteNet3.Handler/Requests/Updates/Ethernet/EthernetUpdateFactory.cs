namespace Toletus.LiteNet3.Handler.Requests.Updates.Ethernet;

public class EthernetUpdateFactory
{
    public static EthernetUpdate CreateWithIpConfiguration(string ip, string mask, string gateway)
        => new() { Update = "ethernet", Data = new { ip, mask, gateway } };

    public static EthernetUpdate CreateWithStaticIp(bool staticIp)
        => new() { Update = "ethernet", Data = new { staticIp } };

    public static EthernetUpdate CreateWithMacAddress(string macAddress)
    {
        var mac = macAddress.Split(":");
        var macArray = mac.Select(x => Convert.ToInt32(x, 16)).ToArray();
        return new() { Update = "ethernet", Data = new { mac = macArray } };
    }
}