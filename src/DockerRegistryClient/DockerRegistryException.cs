﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using DockerRegistry.Models;
using Microsoft.Rest;

namespace DockerRegistry
{
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
}
