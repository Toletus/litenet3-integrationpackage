namespace Toletus.LiteNet3.Handler.Requests.Updates.LiteNet3;

public class LiteNet3UpdateFactory
{
    public static LiteNet3Update CreateWithReleaseTime(int releaseTime)
        => new() { Update = "litenet3", Data = new { releaseTime } };

    public static LiteNet3Update CreateWithAlias(string alias)
        => new() { Update = "litenet3", Data = new { alias } };

    public static LiteNet3Update CreateWithId(int id)
        => new() { Update = "litenet3", Data = new { id } };

    public static LiteNet3Update CreateWithMenuPass(string menuPass)
        => new() { Update = "litenet3", Data = new { menuPass } };
}