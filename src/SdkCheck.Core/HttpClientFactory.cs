namespace SdkCheck;

static class HttpClientFactory
{
    // One client for the life of the process. Per request timeouts come from a CancellationToken so
    // a single client can serve callers configured differently.
    static readonly Lazy<HttpClient> shared = new(() =>
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
        client.DefaultRequestHeaders.Add("User-Agent", "SdkCheck");
        return client;
    });

    public static HttpClient Get() => shared.Value;
}
