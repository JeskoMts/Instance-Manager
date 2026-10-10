using System;
using System.Net;
using System.Net.Http;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace InstanceManager.Composition;

public static class ServiceCollectionExtensions
{
    internal const int MaxResponseContentBytes = 4 * 1024 * 1024;

    public static IServiceCollection AddInstanceManager(this IServiceCollection services)
    {
        services.AddSingleton(_ => CreateHttpClient());
        services.AddSingleton<DpapiSecureStore>();

        services.AddSingleton<IAccountRepository, AccountRepository>();
        services.AddSingleton<IGroupRepository, GroupRepository>();
        services.AddSingleton<IFavoriteRepository, FavoriteRepository>();
        services.AddSingleton<IThemeRepository, ThemeRepository>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ThemeService>();

        services.AddSingleton<RobloxAuthService>();
        services.AddSingleton<IRobloxExecutableValidator, RobloxExecutableValidator>();
        services.AddSingleton<IRobloxAvatarService>(sp =>
            new RobloxAvatarService(sp.GetRequiredService<HttpClient>(), AppPaths.AvatarCacheDirectory));
        services.AddSingleton<IRobloxGamesService, RobloxGamesService>();
        services.AddSingleton<RobloxLauncher>();
        services.AddSingleton<VersionService>();
        services.AddSingleton<InstanceTracker>();
        services.AddSingleton<AntiAfkService>();
        services.AddSingleton<MultiInstanceManager>();
        services.AddSingleton<AutoReconnectLog>();
        services.AddSingleton<AutoReconnectService>();
        services.AddSingleton<LaunchService>();
        services.AddSingleton<RobloxShareLinkService>();
        services.AddSingleton<IServerLinkResolver, ServerLinkResolver>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<IAutomationRepository, AutomationRepository>();
        services.AddSingleton<RobloxGraphicsService>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<ISessionChecker, SessionCheckService>();

        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }

    private static HttpClient CreateHttpClient() =>
        CreateHttpClient(new SocketsHttpHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false
        });

    internal static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20),
            MaxResponseContentBufferSize = MaxResponseContentBytes
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Roblox/WinInet");
        return http;
    }
}
