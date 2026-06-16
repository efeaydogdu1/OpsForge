using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsForge.Application;

namespace OpsForge.Modules.GitHub;

public static class GitHubServiceCollectionExtensions
{
    public static IServiceCollection AddGitHubModule(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new GitHubOptions();
        configuration.GetSection("GitHub").Bind(options);

        services.AddSingleton(options);
        services.AddHttpClient<IGitHubApiClient, GitHubApiClient>();
        services.AddSingleton<IGitHubRepositoryUrlParser, GitHubRepositoryUrlParserAdapter>();

        return services;
    }
}
