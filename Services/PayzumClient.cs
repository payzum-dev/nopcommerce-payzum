using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nop.Plugin.Payments.Payzum.Services;

/// <summary>
/// Payzum REST client + IPN signature verifier.
///
/// Contract (source: https://merchant.payzum.com/docs):
///   Base URL : https://merchant.payzum.com
///   Auth     : header x-api-key: &lt;64-hex key&gt;
///   Create   : POST /v1/payment { price_amount, price_currency, pay_currency, order_id, ipn_callback_url } -> 201 { payment_id, invoice_url }
///   IPN sig  : HMAC-SHA-512(secret, raw_body_bytes) lowercase hex.
/// </summary>
public class PayzumClient
{
    private const string BaseUrl = "https://merchant.payzum.com";

    private readonly IHttpClientFactory _httpClientFactory;

    public PayzumClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Create a hosted-checkout invoice. Returns the parsed response, or null on error.
    /// </summary>
    public async Task<PayzumInvoice?> CreatePaymentAsync(string apiKey, object payload)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/payment");
        request.Headers.Add("x-api-key", apiKey.Trim());
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonSerializer.Deserialize<PayzumInvoice>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Constant-time HMAC-SHA-512 verification over the raw request body.
    /// </summary>
    public static bool VerifySignature(string rawBody, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            return false;

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }
}

/// <summary>Subset of the Payzum payment object we consume.</summary>
public class PayzumInvoice
{
    // Payzum returns the payment id as `payment_id` (alias `id` in older docs).
    public string? Payment_Id { get; set; }
    public string? Id { get; set; }
    public string? Invoice_Url { get; set; }
    public string? Payment_Status { get; set; }

    /// <summary>The payment id, preferring `payment_id` with an `id` fallback.</summary>
    public string? PaymentId => !string.IsNullOrEmpty(Payment_Id) ? Payment_Id : Id;
}
