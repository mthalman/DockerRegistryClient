﻿using Microsoft.Rest;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;
 
public static class TagOperationsExtensions
{
    public static async Task<Page<RepositoryTags>> GetAsync
        (this ITagOperations operations, string repositoryName, int? count = null, CancellationToken cancellationToken = default)
    {
        using HttpOperationResponse<Page<RepositoryTags>> response =
            await operations.GetWithHttpMessagesAsync(repositoryName, count, cancellationToken).ConfigureAwait(false);
        return response.Body;
    }

    public static async Task<Page<RepositoryTags>> GetNextAsync
        (this ITagOperations operations, string nextPageLink, CancellationToken cancellationToken = default)
    {
        using HttpOperationResponse<Page<RepositoryTags>> response =
            await operations.GetNextWithHttpMessagesAsync(nextPageLink, cancellationToken).ConfigureAwait(false);
        return response.Body;
    }
}
