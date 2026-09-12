using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Payzum;

/// <summary>
/// Persisted settings for the Payzum payment method.
/// </summary>
public class PayzumPaymentSettings : ISettings
{
    /// <summary>Payzum API key (64-hex).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Crypto/stablecoin to charge, e.g. usdttrc20.</summary>
    /// <summary>IPN signing secret (verifies HMAC-SHA-512).</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Header Payzum sends the signature in.</summary>
    public string SignatureHeader { get; set; } = "x-nowpayments-sig";
}
