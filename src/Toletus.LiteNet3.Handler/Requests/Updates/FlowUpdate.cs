namespace Toletus.LiteNet3.Handler.Requests.Updates;

public class FlowUpdate : UpdateBase
{
    public FlowUpdate(bool inverted, string @in, string @out, string frontWait, int pictoWaitIn, int pictoWaitOut)
    {
        Update = "flow";
        Data = new
        {
            inverted,
            In = @in,
            Out = @out,
            frontWait,
            pictoWaitIn,
            pictoWaitOut,
        };
    }
}