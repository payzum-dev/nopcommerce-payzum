using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Payzum.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Payments.Payzum.ApiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Payments.Payzum.WebhookSecret")]
    public string WebhookSecret { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Payments.Payzum.SignatureHeader")]
    public string SignatureHeader { get; set; } = "x-nowpayments-sig";

    /// <summary>Read-only IPN URL shown to the merchant to paste into the Payzum dashboard.</summary>
    public string IpnUrl { get; set; } = string.Empty;
}
