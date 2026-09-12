using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Payzum.Components;

/// <summary>Public payment-info component. Payzum is a redirection method, so this is empty.</summary>
public class PayzumViewComponent : NopViewComponent
{
    public IViewComponentResult Invoke()
        => View("~/Plugins/Payments.Payzum/Views/PaymentInfo.cshtml");
}
