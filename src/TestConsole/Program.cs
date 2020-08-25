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
            string registry = args[0];
            BasicAuthenticationCredentials basicCreds = new BasicAuthenticationCredentials
            {
                UserName = args[1],
                Password = args[2]
            };
            
            using DockerRegistryClient client = new DockerRegistryClient(registry, basicCreds);
            //var catalogResponse = await client.Catalog.GetAsync();
            //var tags = await client.Tags.ListAsync("library/alpine");

            var result = await client.Manifests.GetAsync("amd64/alpine", "latest");

        }
    }
}
