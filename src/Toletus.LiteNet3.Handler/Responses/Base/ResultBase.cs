namespace Toletus.LiteNet3.Handler.Responses.Base;

public abstract class ResultBase
{
    public string? Result { get; set; }
    public string? Reason { get; set; }
    public string? Key { get; set; }
}