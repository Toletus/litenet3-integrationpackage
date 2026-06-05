namespace Toletus.LiteNet3.Handler.Requests.Fetches;

public class GenericFetch : FetchBase
{
    public GenericFetch(string fetch)
    {
        if (string.IsNullOrWhiteSpace(fetch))
            throw new ArgumentException("O fetch deve ser informado.", nameof(fetch));
        
        Fetch = fetch;
    }
}