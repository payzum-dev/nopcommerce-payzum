using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Payzum.Components;
using Nop.Plugin.Payments.Payzum.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.Payzum;

/// <summary>
/// Payzum redirection payment method. On order placement the buyer is sent to a Payzum hosted
/// checkout; the order is settled from the signed IPN (see PaymentPayzumController.Ipn).
/// </summary>
public class PayzumPaymentProcessor : BasePlugin, IPaymentMethod
{
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly IWebHelper _webHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PayzumClient _payzumClient;
    private readonly PayzumPaymentSettings _settings;

    public PayzumPaymentProcessor(
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper,
        IHttpContextAccessor httpContextAccessor,
        PayzumClient payzumClient,
        PayzumPaymentSettings settings)
    {
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
        _httpContextAccessor = httpContextAccessor;
        _payzumClient = payzumClient;
        _settings = settings;
    }

    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        => Task.FromResult(new ProcessPaymentResult());

    /// <summary>Create the Payzum invoice for the order and redirect the buyer to it.</summary>
    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        var order = postProcessPaymentRequest.Order;
        var storeUrl = _webHelper.GetStoreLocation();

        // OrderTotal is held in the store's primary currency, while CustomerCurrencyCode is the
        // currency the buyer checked out in. Pairing them directly billed, say, a 100 USD total
        // as "100 EUR". CurrencyRate is nopCommerce's own primary -> customer rate, recorded on
        // the order at checkout — the same one its invoices and emails use — so converting with
        // it charges exactly the figure the buyer was shown.
        var price = Math.Round(order.OrderTotal * order.CurrencyRate, 2, MidpointRounding.AwayFromZero);

        var payload = new
        {
            price_amount = price,
            price_currency = (order.CustomerCurrencyCode ?? "usd").ToLowerInvariant(),
            // "all" defers the coin choice to the buyer on the Payzum hosted checkout, limited to
            // the merchant's allowlist. The plugin never picks a coin.
            pay_currency = "all",
            order_id = order.OrderGuid.ToString(),
            order_description = $"Order {order.CustomOrderNumber} at {storeUrl}",
            ipn_callback_url = $"{storeUrl}Plugins/PaymentPayzum/Ipn",
            success_url = $"{storeUrl}checkout/completed/{order.Id}",
            cancel_url = $"{storeUrl}order/details/{order.Id}"
        };

        var invoice = await _payzumClient.CreatePaymentAsync(_settings.ApiKey, payload);

        if (string.IsNullOrEmpty(invoice?.Invoice_Url))
        {
            // The client returns null for every failure. Falling through silently left the buyer
            // on "checkout completed" for an order they were never given a way to pay: no
            // gateway, no error, an order sitting Pending. Fail loudly instead.
            throw new NopException(
                $"Payzum did not return a hosted checkout URL for order {order.CustomOrderNumber}. " +
                "Check the API key and the plugin log.");
        }

        _httpContextAccessor.HttpContext?.Response.Redirect(invoice!.Invoice_Url!);
    }

    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart) => Task.FromResult(false);

    public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart) => Task.FromResult(decimal.Zero);

    public Task<bool> CanRePostProcessPaymentAsync(Order order) => Task.FromResult(false);

    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        => Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });

    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        => Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });

    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        => Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });

    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        => Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });

    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        => Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });

    public Task<IList<string>> ValidatePaymentFormAsync(Microsoft.AspNetCore.Http.IFormCollection form)
        => Task.FromResult<IList<string>>(new List<string>());

    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(Microsoft.AspNetCore.Http.IFormCollection form)
        => Task.FromResult(new ProcessPaymentRequest());

    public Task<string> GetPaymentMethodDescriptionAsync()
        => Task.FromResult("Pay with USDC/USDT or other crypto via Payzum. Non-custodial.");

    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/PaymentPayzum/Configure";

    public Type GetPublicViewComponent() => typeof(PayzumViewComponent);

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new PayzumPaymentSettings
        {
            SignatureHeader = "x-nowpayments-sig"
        });
        await base.InstallAsync();
    }

    /// <summary>
    /// Clear the stale IPN signature header.
    ///
    /// Payzum signs the IPN with <c>x-nowpayments-sig</c>; earlier releases shipped
    /// <c>x-payzum-signature</c>, which the platform never sends. Verification failed, the IPN got a
    /// 401, and the order stayed unpaid even though the customer had paid. Changing the default was
    /// not enough: nopCommerce keeps whatever was saved, so an existing install stays broken until
    /// somebody edits the field by hand.
    ///
    /// Only the known-bad value is rewritten — anything else set deliberately is left alone.
    /// </summary>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        var settings = await _settingService.LoadSettingAsync<PayzumPaymentSettings>();

        if (settings.SignatureHeader == "x-payzum-signature")
        {
            settings.SignatureHeader = "x-nowpayments-sig";
            await _settingService.SaveSettingAsync(settings);
        }

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<PayzumPaymentSettings>();
        await base.UninstallAsync();
    }

    public bool SupportCapture => false;
    public bool SupportPartiallyRefund => false;
    public bool SupportRefund => false;
    public bool SupportVoid => false;
    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;
    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;
    public bool SkipPaymentInfo => true;
}
