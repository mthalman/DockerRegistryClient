using System;
using System.Threading.Tasks;
using DockerRegistry;
using Microsoft.Rest;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            ExecuteAsync(args).Wait();
        }

        private static async Task ExecuteAsync(string[] args)
        {
            BasicAuthenticationCredentials basicCreds = new BasicAuthenticationCredentials
            {
                UserName = args[1],
                Password = args[2]
            };
            
            using DockerRegistryClient client = new DockerRegistryClient(args[0]);
            //var catalogResponse = await client.Catalog.GetAsync();
            //var tags = await client.Tags.ListAsync("library/alpine");
            var digest = await client.Manifests.GetDigestAsync("library/alpine", "3.12");
        }
    }
}
