using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;

namespace DockerRegistry
{
    public static class TagOperationsExtensions
    {
        public static async Task<RepositoryTags> GetAsync(this ITagOperations operations, string repositoryName, CancellationToken cancellationToken = default)
        {
            var response = await operations.GetWithHttpMessagesAsync(repositoryName, cancellationToken).ConfigureAwait(false);
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
