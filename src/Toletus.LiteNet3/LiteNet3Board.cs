using System.Net;
using Toletus.LiteNet3.Handler.Requests.Actions;
using Toletus.LiteNet3.Handler.Requests.Actions.Buzzer;
using Toletus.LiteNet3.Handler.Requests.Actions.LiteNet3;
using Toletus.LiteNet3.Handler.Requests.Fetches;
using Toletus.LiteNet3.Handler.Requests.Updates;
using Toletus.LiteNet3.Handler.Requests.Updates.Ethernet;
using Toletus.LiteNet3.Handler.Requests.Updates.LiteNet3;
using Toletus.Pack.Core;

namespace Toletus.LiteNet3;

public class LiteNet3Board : LiteNet3BoardBase
{
    private LiteNet3Board(IPAddress ip, bool connected, int id, string? serial, string? alias)
        : base(ip, connected, id, serial: serial, alias: alias)
    {
        IpConfig = new IpConfig();
    }

    public static LiteNet3Board CreateFromBase(LiteNet3BoardBase boardBase)
    {
        return new LiteNet3Board(
            boardBase.Ip,
            boardBase.Connected,
            boardBase.Id,
            boardBase.Serial,
            boardBase.Alias);
    }

    public override string ToString() =>
        $"{base.ToString()}" + (HasFingerprintReader ? " Bio" : "") + $" {Description}";

    public void ReleaseEntry(string? topRow, string? bottomRow)
    {
        Send(LiteNet3ActionFactory.CreateReleaseAction("In", topRow, bottomRow));
    }

    public void ReleaseExit(string? topRow, string? bottomRow)
    {
        Send(LiteNet3ActionFactory.CreateReleaseAction("Out", topRow, bottomRow));
    }

    public void ReleaseEntryAndExit(string? topRow, string? bottomRow)
    {
        Send(LiteNet3ActionFactory.CreateReleaseAction("All", topRow, bottomRow));
    }

    public void BuzzerPlay(string play)
    {
        Send(BuzzerActionFactory.CreatePlayAction(play));
    }

    public void BuzzerStop()
    {
        Send(BuzzerActionFactory.CreateStopCommand("stop"));
    }

    public void BuzzerMute(bool mute)
    {
        Send(BuzzerActionFactory.CreateMuteCommand(mute));
    }

    public void Notify(string cmd, int time, string alignBot, string topRow, string bottomRow)
    {
        Send(new DispayAction(cmd, time, alignBot, topRow, bottomRow));
    }

    public void Reset()
    {
        Send(LiteNet3ActionFactory.CreateAction(cmd: "reset"));
        Close();
    }

    public void GetFactory()
    {
        Send(new FactoryFetch());
    }

    public void GetFlow()
    {
        Send(new FlowFetch());
    }

    public void GetStatusAndConfigurations()
    {
        Send(new LiteNet3Fetch());
    }

    public void GetDisplay()
    {
        Send(new DisplayFetch());
    }

    public void GetBuzzerMute()
    {
        Send(new BuzzerFetch());
    }

    public void GetEthernet()
    {
        Send(new EthernetFetch());
    }

    public void GetSensor()
    {
        Send(new SensorFetch());
    }

    public void SetBuzzerMute(bool mute)
    {
        Send(new BuzzerUpdate(mute));
    }

    public void SetFlow(
        bool inverted,
        string @in,
        string @out,
        string frontWait,
        int pictoWaitIn,
        int pictoWaitOut)
    {
        Send(new FlowUpdate(inverted, @in, @out, frontWait, pictoWaitIn, pictoWaitOut));
    }

    public void SetIpConfigurartion(string ip, string mask, string gateway)
    {
        Send(EthernetUpdateFactory.CreateWithIpConfiguration(ip, mask, gateway));
    }

    public void SetStaticIp(bool staticIp)
    {
        Send(EthernetUpdateFactory.CreateWithStaticIp(staticIp));
    }

    public void SetMacAddress(string macAddress)
    {
        Send(EthernetUpdateFactory.CreateWithMacAddress(macAddress));
    }

    public void SetId(int id)
    {
        Id = id;
        Send(LiteNet3UpdateFactory.CreateWithId(id));
    }

    public void SetAlias(string alias)
    {
        Send(LiteNet3UpdateFactory.CreateWithAlias(alias));
    }

    public void SetDisplay(string? topRow, string? bottomRow, string? mode)
    {
        Send(new DisplayUpdate(topRow, bottomRow, mode));
    }

    public void SetReleaseDuration(int releaseDuration)
    {
        Send(LiteNet3UpdateFactory.CreateWithReleaseTime(releaseDuration));
    }

    public void ResetSensor(bool resetIn, bool resetOut)
    {
        Send(new SensorUpdate(resetIn, resetOut));
    }

    public void SetMenuPassword(string password)
    {
        Send(LiteNet3UpdateFactory.CreateWithMenuPass(password));
    }
}