﻿using System.Threading;
using System.Threading.Tasks;
using DockerRegistry.Models;
using Microsoft.Rest;

namespace DockerRegistry
{
    public static class CatalogOperationsExtensions
    {
        public static async Task<Page<Catalog>> GetAsync(this ICatalogOperations operations, int? count = null,
            CancellationToken cancellationToken = default)
        {
            using HttpOperationResponse<Page<Catalog>>? response =
                await operations.GetWithHttpMessagesAsync(count, cancellationToken).ConfigureAwait(false);
            return response.Body;
        }

        public static async Task<Page<Catalog>> GetNextAsync
            (this ICatalogOperations operations, string nextPageLink, CancellationToken cancellationToken = default)
        {
            using HttpOperationResponse<Page<Catalog>> response =
                await operations.GetNextWithHttpMessagesAsync(nextPageLink, cancellationToken).ConfigureAwait(false);
            return response.Body;
        }
    }
}
