﻿using Microsoft.Rest;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

public class DockerRegistryException : HttpOperationException
{
    public DockerRegistryException()
    {
    }

    public DockerRegistryException(string message)
        : base(message)
    {
    }
        
    public DockerRegistryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public IEnumerable<Error> Errors { get; set; } = Enumerable.Empty<Error>();
}
