using Newtonsoft.Json.Linq;

namespace Toletus.LiteNet3.Handler;

public static class JObjectUtil
{
    public static JObject? Parse(string obj)
    {
        try
        {
            return JObject.Parse(obj);
        }
        catch (Exception)
        {
            return null;
        }
    }
}