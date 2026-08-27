using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OmniBusiness.Application.Abstractions.Compliance;
using OmniBusiness.Application.Abstractions.Persistence;
using OmniBusiness.Application.Abstractions.Security;
using OmniBusiness.Infrastructure.Compliance;
using OmniBusiness.Application.Services;
using OmniBusiness.Infrastructure.EmbeddedSeed;
using OmniBusiness.Infrastructure.Persistence;
using OmniBusiness.Infrastructure.Security;

namespace OmniBusiness.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOmniBusinessFoundation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var persistenceOptions = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>()
            ?? new PersistenceOptions();

        var localAppDataRoot = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrWhiteSpace(localAppDataRoot))
        {
            localAppDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        var keysRoot = WritableStoragePathResolver.ResolveWritableDirectory(
            environment,
            Path.Combine(localAppDataRoot, "OmniBusiness", "keys"),
            Path.Combine(environment.ContentRootPath, ".omnibusiness-runtime", "keys"),
            Path.Combine(Path.GetTempPath(), "OmniBusiness", "keys"));

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysRoot))
            .SetApplicationName("OmniBusiness");
        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName));
        services.AddOptions<FbrOptions>()
            .Bind(configuration.GetSection(FbrOptions.SectionName));

        services.AddSingleton<IWorkspaceRepository>(_ =>
            persistenceOptions.Provider switch
            {
                "EmbeddedSeed" => new EmbeddedWorkspaceRepository(environment),
                "LocalJson" => ActivatorUtilities.CreateInstance<LocalJsonWorkspaceRepository>(_),
                "Supabase" or "Postgres" => ActivatorUtilities.CreateInstance<PostgresWorkspaceRepository>(_),
                _ => throw new InvalidOperationException(
                    $"Unsupported persistence provider '{persistenceOptions.Provider}'.")
            });
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, ProtectedTokenService>();
        services.AddSingleton<IFbrInvoiceService, OfflineCapableFbrInvoiceService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWorkspaceQueryService, WorkspaceQueryService>();
        services.AddScoped<IPosWorkflowService, PosWorkflowService>();
        services.AddScoped<IInventoryManagementService, InventoryManagementService>();
        services.AddScoped<ICustomizationCommandService, CustomizationCommandService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IModuleManagementService, ModuleManagementService>();

        services
            .AddAuthentication(OmniBusinessAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, OmniBusinessBearerAuthenticationHandler>(
                OmniBusinessAuthenticationDefaults.Scheme,
                _ => { });

        return services;
    }
}
