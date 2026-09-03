using System.Runtime.CompilerServices;

namespace Valleysoft.DockerRegistryClient;

internal static class PaginationHelper
{
    public static async IAsyncEnumerable<Page<T>> GetPagesAsync<T>(
        Func<CancellationToken, Task<Page<T>>> getFirstPageAsync,
        Func<string, CancellationToken, Task<Page<T>>> getNextPageAsync,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Page<T> page = await getFirstPageAsync(cancellationToken).ConfigureAwait(false);

        while (true)
        {
            yield return page;

            if (page.NextPageLink is null)
            {
                yield break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            page = await getNextPageAsync(page.NextPageLink, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async IAsyncEnumerable<TItem> GetItemsAsync<TPage, TItem>(
        IAsyncEnumerable<Page<TPage>> pages,
        Func<TPage, IEnumerable<TItem>> getItems,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (Page<TPage> page in pages
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (TItem item in getItems(page.Value))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
        }
    }
}
