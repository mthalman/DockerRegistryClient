using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;

namespace DockerRegistry
{
    public static class CatalogOperationsExtensions
    {
        public static async Task<Catalog> GetAsync(this ICatalogOperations operations, CancellationToken cancellationToken = default)
        {
            var response = await operations.GetWithHttpMessagesAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return response.Body;
            }
            finally
            {
                response.Dispose();
            }
        }
    }
}
