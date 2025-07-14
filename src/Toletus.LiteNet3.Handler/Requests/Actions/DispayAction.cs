namespace Toletus.LiteNet3.Handler.Requests.Actions;

public class DispayAction : ActionBase
{
    public DispayAction(
        string cmd,
        int time,
        string alignBot,
        string topRow = "Toletus",
        string bottomRow = "Bem vindo!")
    {
        Action = "display";
        Data = new
        {
            cmd,
            time,
            topRow,
            bottomRow,
            alignBot,
        };
    }
}