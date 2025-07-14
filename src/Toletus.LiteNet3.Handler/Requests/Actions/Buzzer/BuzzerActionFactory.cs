namespace Toletus.LiteNet3.Handler.Requests.Actions.Buzzer;

public static class BuzzerActionFactory
{
    public static BuzzerAction CreatePlayAction(string play) => new() { Action = "buzzer", Data = new { play } };

    public static BuzzerAction CreateStopCommand(string stop) => new() { Action = "buzzer", Data = new { cmd = stop } };

    public static BuzzerAction CreateMuteCommand(bool mute) => new() { Action = "buzzer", Data = new { mute } };
}