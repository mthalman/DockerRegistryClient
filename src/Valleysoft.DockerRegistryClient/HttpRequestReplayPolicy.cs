namespace Valleysoft.DockerRegistryClient;

internal static class HttpRequestReplayPolicy
{
    public static void PrepareForReplay(HttpRequestMessage request)
    {
        if (request.Content is null)
        {
            return;
        }

        Type contentType = request.Content.GetType();
        if (contentType == typeof(ByteArrayContent) ||
            contentType == typeof(StringContent) ||
            contentType == typeof(FormUrlEncodedContent)
#if NET5_0_OR_GREATER
            || contentType == typeof(ReadOnlyMemoryContent)
#endif
            )
        {
            return;
        }

        if (request.Content is IReplayableHttpContent replayableContent)
        {
            replayableContent.PrepareForReplay();
            return;
        }

        throw new InvalidOperationException(
            $"Request content of type '{request.Content.GetType().Name}' cannot be safely replayed.");
    }
}
