﻿using System.Threading;
using System.Threading.Tasks;
using Valleysoft.DockerRegistryClient.Models;
using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient
{
    public interface ITagOperations
    {
        Task<HttpOperationResponse<Page<RepositoryTags>>> GetWithHttpMessagesAsync(
            string repositoryName, int? count = null, CancellationToken cancellationToken = default);

        Task<HttpOperationResponse<Page<RepositoryTags>>> GetNextWithHttpMessagesAsync(
            string nextPageLink, CancellationToken cancellationToken = default);
    }
}
