using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Payzum.Infrastructure;

/// <summary>Registers the public Payzum IPN route.</summary>
public class RouteProvider : IRouteProvider
{
    public int Priority => 0;

    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Payments.Payzum.Ipn",
            pattern: "Plugins/PaymentPayzum/Ipn",
            defaults: new { controller = "PaymentPayzumIpn", action = "Ipn" });
    }
}
