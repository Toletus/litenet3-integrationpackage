namespace Toletus.LiteNet3.Handler.Requests.Actions.LiteNet3;

public static class LiteNet3ActionFactory
{
    private const string TopRow = "Toletus";
    private const string BottomRow = "Bem vindo!";

    public static LiteNet3Action CreateReleaseAction(string release, string? topRow = null, string? bottomRow = null)
        => new()
        {
            Action = "litenet3",
            Data = new
            {
                release,
                topRow = string.IsNullOrEmpty(topRow) ? TopRow : topRow,
                bottomRow = string.IsNullOrEmpty(bottomRow) ? BottomRow : bottomRow,
            }
        };

    public static LiteNet3Action CreateAction(string cmd) => new() { Action = "litenet3", Data = new { cmd } };
}