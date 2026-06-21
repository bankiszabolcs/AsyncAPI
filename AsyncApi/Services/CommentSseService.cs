using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AsyncApi.Services;

public sealed class CommentSseService
{
    // videoId → (connId → channel)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Channel<string>>> _connections = new();

    public (string ConnId, ChannelReader<string> Reader) Subscribe(Guid videoId)
    {
        var ch     = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        var connId = Guid.NewGuid().ToString("N");
        _connections.GetOrAdd(videoId, _ => new())[connId] = ch;
        return (connId, ch.Reader);
    }

    public void Unsubscribe(Guid videoId, string connId)
    {
        if (_connections.TryGetValue(videoId, out var conns))
        {
            conns.TryRemove(connId, out var ch);
            ch?.Writer.TryComplete();
        }
    }

    public async ValueTask BroadcastAsync(Guid videoId, string json)
    {
        if (!_connections.TryGetValue(videoId, out var conns)) return;
        foreach (var (_, ch) in conns)
            await ch.Writer.WriteAsync(json);
    }
}
