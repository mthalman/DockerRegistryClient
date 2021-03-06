﻿using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient
{
    public interface IBlobOperations
    {
        Task<HttpOperationResponse<Stream>> GetWithHttpMessagesAsync(
            string repositoryName, string digest, CancellationToken cancellationToken = default);
    }
}
