namespace Toletus.LiteNet3.Handler.Requests.Updates;

public class DisplayUpdate : UpdateBase
{
    public DisplayUpdate(
        string? topRow = "    Toletus     ",
        string? bottomRow = "   Bem vindo!   ",
        string? mode = "message")
    {
        Update = "display";
        Data = new
        {
            topRow,
            bottomRow,
            mode,
        };
    }
}