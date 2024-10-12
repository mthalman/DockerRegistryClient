# Docker Registry Client

*A .NET client interface for executing commands on a Docker registry's HTTP API.*

## Example Usage

```csharp
RegistryClient client = new("mcr.microsoft.com");
Page<RepositoryTags> tagsPage = await client.Tags.GetAsync("dotnet/sdk");
foreach (string tag in tagsPage.Value.Tags)
{
  Console.WriteLine(tag);
}
```
