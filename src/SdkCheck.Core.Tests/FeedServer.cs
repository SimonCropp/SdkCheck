/// <summary>
/// Serves one channel document over loopback, so a test can reach Fetch - and the cache write under
/// it - without the network and without a fixed port.
/// </summary>
/// <remarks>
/// A socket rather than HttpListener, which wants a url reservation on Windows and fails with
/// "Access is denied" for anyone who has not made one. What is spoken here is the small part of
/// HTTP/1.1 that HttpClient needs to hand a body back: a status line, a length, and a close.
/// </remarks>
public sealed class FeedServer : IDisposable
{
    readonly TcpListener listener;
    readonly byte[] body;
    int requests;

    public FeedServer(string channel)
    {
        body = File.ReadAllBytes(Path.Combine(Feeds.Directory, $"{channel}.json"));
        listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}";
        _ = Task.Run(Serve);
    }

    /// <summary>
    /// What to give <see cref="FeedOptions.BaseUrl"/>.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// How many times the feed has actually been fetched, which is how a test tells a fetch from a
    /// cache read.
    /// </summary>
    public int Requests => Volatile.Read(ref requests);

    async Task Serve()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                // Disposed. Nothing else stops this loop.
                return;
            }

            using (client)
            {
                Interlocked.Increment(ref requests);
                try
                {
                    var stream = client.GetStream();
                    await ReadRequest(stream);

                    var head = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(head, 0, head.Length);
                    await stream.WriteAsync(body, 0, body.Length);
                    await stream.FlushAsync();
                }
                catch (Exception)
                {
                    // A client that went away mid-response is the client's business, and failing a
                    // background loop here would surface as an unrelated test hanging.
                }
            }
        }
    }

    /// <summary>
    /// Reads to the end of the request headers. The body is never read: nothing here takes one, and
    /// a response cannot be written until the request has been taken off the socket.
    /// </summary>
    static async Task ReadRequest(NetworkStream stream)
    {
        var buffer = new byte[1024];
        var text = new StringBuilder();
        while (!text.ToString().Contains("\r\n\r\n"))
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
            {
                return;
            }

            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }
    }

    public void Dispose() =>
        listener.Stop();
}
