namespace Toletus.LiteNet3.Handler.Requests.Updates;

public class BuzzerUpdate : UpdateBase
{
    public BuzzerUpdate(bool mute)
    {
        Update = "buzzer";
        Data = new { mute };
    }
}