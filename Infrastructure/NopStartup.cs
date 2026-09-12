using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Payzum.Services;

namespace Nop.Plugin.Payments.Payzum.Infrastructure;

/// <summary>
/// Registers the plugin's own services with the host container.
///
/// Without this, nopCommerce cannot construct PayzumPaymentProcessor — PayzumClient is not a
/// known dependency, the constructor cannot be satisfied, and installing the plugin fails with
/// "No constructor was found that had all the dependencies satisfied ---> Unknown dependency".
/// The plugin is discovered and listed in the admin, but can never be installed.
/// </summary>
public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // PayzumClient resolves an IHttpClientFactory and calls CreateClient() per request, so
        // it is a plain scoped service — not a typed client. AddHttpClient<PayzumClient>() would
        // register it as a typed client and then fail to activate it, because that pattern
        // requires a constructor taking HttpClient.
        services.AddHttpClient();
        services.AddScoped<PayzumClient>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    /// <summary>Runs after the framework's own registrations, as plugin startups do.</summary>
    public int Order => 3000;
}
