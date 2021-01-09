using System.CommandLine;

namespace Valleysoft.DockerRegistryClient.Cli
{
    public class Program
    {
        

        public static int Main(string[] args)
        {
            RootCommand rootCmd = new RootCommand
            {
                new RepoCommand(),
                new TagCommand(),
                new ManifestCommand()
            };

            return rootCmd.Invoke(args);
        }
    }
}
