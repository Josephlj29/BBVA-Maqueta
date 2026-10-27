using Bbva.Reconciliation.Application.Ports;
using Bbva.Reconciliation.Application.Sandbox;
using Bbva.Reconciliation.Infrastructure.Auth;
using Bbva.Reconciliation.Infrastructure.Bbva;
using Bbva.Reconciliation.Infrastructure.Configuration;
using Bbva.Reconciliation.Infrastructure.Http;
using Bbva.Reconciliation.Infrastructure.Sandbox;
using Bbva.Reconciliation.Infrastructure.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bbva.Reconciliation.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Named client used for the OAuth token endpoint.</summary>
    public const string TokenClientName = "bbva-token";

    /// <summary>
    /// Registers the BBVA adapter: options validated at startup, the signing key, the
    /// token provider and the typed transactions client.
    /// </summary>
    public static IServiceCollection AddBbvaIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<BbvaOptions>()
            .Bind(configuration.GetSection(BbvaOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !options.ValidateKeySource().Any(),
                "Configure exactly one of Bbva:PrivateKeyPath or Bbva:PrivateKeyBase64.")
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "Bbva:BaseUrl must be an absolute URL.")
            .ValidateOnStart();

        services.TryAddSingletonTimeProvider();

        // The key is loaded once and shared: importing a PEM on every request, as the
        // reference client did, is pure waste and turns a config fault into a per-request
        // failure instead of a startup failure.
        services.AddSingleton<IPrivateKeyProvider, PemPrivateKeyProvider>();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BbvaOptions>>();

            return new RsaSha512RequestSigner(
                serviceProvider.GetRequiredService<IPrivateKeyProvider>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                options.Value.IncludeSignatureExpires);
        });

        services.AddTransient<BbvaSigningHandler>();

        // The token client is intentionally free of the signing handler: the token
        // endpoint authenticates with the client secret and must not be signed.
        services.AddHttpClient<IBbvaTokenProvider, BbvaOAuthTokenProvider>(TokenClientName, ConfigureBaseAddress)
            .AddStandardResilienceHandler();

        services.AddHttpClient<IBbvaTransactionsPort, BbvaTransactionsClient>(ConfigureBaseAddress)
            .AddHttpMessageHandler<BbvaSigningHandler>()
            .AddStandardResilienceHandler();

        services.AddSingleton<ISignaturePreviewService, SignaturePreviewService>();

        return services;
    }

    /// <summary>
    /// Registers the sandbox certification runner: the extracted test plan plus a raw
    /// request executor that shares the signed pipeline but bypasses domain validation.
    /// </summary>
    /// <param name="contentRootPath">Base path for resolving a relative test plan path.</param>
    public static IServiceCollection AddSandboxTestPlan(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SandboxOptions>(configuration.GetSection(SandboxOptions.SectionName));

        services.AddSingleton<ISandboxTestPlanRepository>(serviceProvider =>
            new JsonFileSandboxTestPlanRepository(
                serviceProvider.GetRequiredService<IOptions<SandboxOptions>>(),
                contentRootPath));

        // Deliberately malformed scenario bodies must reach BBVA untouched, so this client
        // shares the signing pipeline but not the typed client's validation.
        services.AddHttpClient<ISandboxRequestExecutor, HttpSandboxRequestExecutor>(ConfigureBaseAddress)
            .AddHttpMessageHandler<BbvaSigningHandler>()
            .AddStandardResilienceHandler();

        services.AddScoped<RunSandboxCaseHandler>();

        return services;
    }

    private static void ConfigureBaseAddress(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<BbvaOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = options.RequestTimeout;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
