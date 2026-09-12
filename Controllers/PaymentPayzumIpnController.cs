using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Payzum.Services;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.Payzum.Controllers;

/// <summary>
/// Public, HMAC-authenticated Payzum IPN endpoint. Verifies the HMAC-SHA-512 signature over the raw
/// body before parsing, then marks the order paid on `finished`. (No admin/antiforgery — this is a
/// server-to-server webhook, authenticated by the signature.)
/// </summary>
public class PaymentPayzumIpnController : Controller
{
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly PayzumPaymentSettings _settings;

    public PaymentPayzumIpnController(
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        PayzumPaymentSettings settings)
    {
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _settings = settings;
    }

    [HttpPost]
    public async Task<IActionResult> Ipn()
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body))
            rawBody = await reader.ReadToEndAsync();

        if (string.IsNullOrEmpty(rawBody))
            return BadRequest("empty body");

        // The header name is fixed, not read from settings.
        //
        // `x-payzum-signature` is a DIFFERENT webhook family — it signs mass payouts, with
        // HMAC-SHA-256 over an unsorted body. Reading it for a payment IPN is why 20 of the 21
        // plugins used to fail verification, and a configurable field is an invitation to fill it
        // with that value. Every other integration dropped the setting during the SDK migration;
        // this one has no SDK, so the constant lives here. SignatureHeader is kept in settings only
        // so existing installs upgrade cleanly, and is deliberately ignored.
        var signature = Request.Headers["x-nowpayments-sig"].ToString();

        if (!PayzumClient.VerifySignature(rawBody, signature, _settings.WebhookSecret))
            return Unauthorized();

        PayzumIpnPayload? data;
        try
        {
            data = System.Text.Json.JsonSerializer.Deserialize<PayzumIpnPayload>(rawBody, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return BadRequest("bad json");
        }

        if (data == null || string.IsNullOrEmpty(data.Order_Id) || !Guid.TryParse(data.Order_Id, out var orderGuid))
            return NotFound("no order");

        var order = await _orderService.GetOrderByGuidAsync(orderGuid);
        if (order == null)
            return NotFound("order not found");

        // The merchant surface emits exactly five values: waiting, partially_paid, finished,
        // expired, failed. There is no "cancelled" (it arrives as `failed`) and no "confirming",
        // "confirmed", "sending" or "refunded" — those exist only in the NowPayments-compatible
        // type and are unreachable. `partially_paid` is a deliberate no-op: the order stays Pending
        // while the buyer tops up, and a later `finished` still marks it paid (CanMarkOrderAsPaid
        // keeps that idempotent). Anything unrecognised falls through untouched.
        var status = (data.Payment_Status ?? "").ToLowerInvariant();

        if (status == "finished")
        {
            // What the invoice was created for: the order total converted with the rate
            // nopCommerce recorded at checkout (see PayzumPaymentProcessor). A notification
            // reporting anything else is not this order's money — mark nothing and say so,
            // because no retry will make the figures agree.
            var expected = Math.Round(order.OrderTotal * order.CurrencyRate, 2, MidpointRounding.AwayFromZero);
            var paidAmount = data.Amount();
            var paidCurrency = (data.Price_Currency ?? "").ToLowerInvariant();
            var orderCurrency = (order.CustomerCurrencyCode ?? "").ToLowerInvariant();

            // Half a cent: wide enough for the decimal round-trip, far too narrow to hide a
            // discount.
            if (paidAmount == null || Math.Abs(paidAmount.Value - expected) > 0.005m ||
                paidCurrency != orderCurrency)
            {
                return Ok("amount mismatch");
            }

            if (_orderProcessingService.CanMarkOrderAsPaid(order))
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
        }
        else if (status is "failed" or "expired")
        {
            // A paid order is never cancelled by a late terminal event.
            //
            // CanCancelOrder() does NOT protect against this: it returns false only for an order
            // that is already cancelled, so a PAID order passes it. Payzum retries a delivery up to
            // six times and fires on more than one transition, so a late `expired` for an invoice
            // that was ultimately paid is ordinary traffic — and cancelling in nopCommerce restocks
            // the inventory and can trigger a refund. Verified on 4.70.3: order 4, Complete/Paid,
            // was moved to Cancelled by a signed late `expired`.
            if (order.PaymentStatus == PaymentStatus.Paid)
                return Ok("ok");

            if (_orderProcessingService.CanCancelOrder(order))
                await _orderProcessingService.CancelOrderAsync(order, true);
        }

        return Ok("ok");
    }
}

public class PayzumIpnPayload
{
    public string? Order_Id { get; set; }
    public string? Payment_Status { get; set; }
    // Payzum returns the payment id as `payment_id` (alias `id` in older docs).
    public string? Payment_Id { get; set; }
    public string? Id { get; set; }

    // JsonElement, not string: the contract types price_amount as a JSON *number*
    // (`"price_amount": 3.00`), and System.Text.Json throws when a number lands on a
    // string property — which would turn every delivery, of every status, into a 400.
    // Kept permissive so a future string encoding still reads.
    public JsonElement? Price_Amount { get; set; }
    public string? Price_Currency { get; set; }

    /// <summary>The invoiced amount, whether it arrives as a JSON number or a string.</summary>
    public decimal? Amount()
    {
        if (Price_Amount is not { } element)
            return null;

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out var number) ? number : null,
            JsonValueKind.String => decimal.TryParse(
                element.GetString(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            _ => null,
        };
    }

    /// <summary>The payment id, preferring `payment_id` with an `id` fallback.</summary>
    public string? PaymentId => !string.IsNullOrEmpty(Payment_Id) ? Payment_Id : Id;
}
