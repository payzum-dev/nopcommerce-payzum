# Payzum for nopCommerce — Accept Crypto & Stablecoin Payments (USDC, USDT)

Accept **cryptocurrency and stablecoin payments** (USDC, USDT and more, multi-chain) in
[nopCommerce](https://www.nopcommerce.com) through [Payzum](https://payzum.com) —
**non-custodial**: funds settle directly to your own wallet, Payzum never takes
custody. No chargebacks, no card networks, no PCI surface.

- **Plugin:** `Nop.Plugin.Payments.Payzum` · **Version:** 1.1.0 · **License:** MIT
- **Requires:** nopCommerce **4.70** (.NET 8)
- **Type:** redirection payment method (C#, hand-written REST client — no external NuGet dependency)

## How it works

1. The buyer picks **Payzum — Crypto & Stablecoins** at checkout and is
   redirected to a hosted checkout page (QR code + deposit address, live
   status), where they choose the coin and chain and send the payment. No
   wallet or card data touches your server.
2. Crypto confirmation is **asynchronous**, so the order is marked paid from
   Payzum's signed server-to-server IPN webhook, never from the buyer's browser
   return — a closed tab never loses a paid order.
3. Every webhook is verified with **HMAC-SHA-512 over the raw request bytes**
   (`CryptographicOperations.FixedTimeEquals`) before a single field of it is
   read, and the delivered **amount and currency are compared against the
   order total** before `MarkOrderAsPaidAsync` — a mismatch never marks the
   order paid. Redelivered webhooks are a no-op, and a late `expired` delivery
   can never cancel (and restock) an order that was already paid.

## Features

- **Stablecoin-first**: USDC and USDT across multiple chains (Polygon, Ethereum,
  Arbitrum, Base, Optimism, Tron, Solana and more), plus major cryptocurrencies.
- **Non-custodial** — payments settle to the merchant's own wallet.
- **Hosted checkout** — no card fields, no crypto handling, no PCI scope. The
  buyer picks the coin on the Payzum checkout (`pay_currency: "all"`), limited
  to your merchant allowlist and enforced server-side.
- **Amount verification on settlement** — `finished` alone is not enough; the
  IPN's amount and currency must match the order.
- **Zero chargebacks** — crypto payments are final.

## Installation

1. Get the source: download
   [`payzum-nopcommerce-src-1.1.0.zip`](https://github.com/payzum-dev/nopcommerce-payzum/releases/latest)
   and unzip it into `src/Plugins/` (the archive contains the
   `Nop.Plugin.Payments.Payzum/` folder), or clone this repository into
   `src/Plugins/Nop.Plugin.Payments.Payzum/` — the repository *is* the project, so it needs that
   folder name.
2. Build the solution — the csproj outputs the plugin into
   `Presentation/Nop.Web/Plugins/Payments.Payzum`.
3. **Admin → Configuration → Local plugins → Payzum → Install**, then
   **Configure**.
4. Fill in the **API key** and **Webhook secret** from your
   [Payzum merchant dashboard](https://merchant.payzum.com).
5. Copy the shown **IPN URL** (`https://<your-store>/Plugins/PaymentPayzum/Ipn`)
   into the Payzum dashboard webhook settings.

## Configuration

| Setting | Meaning |
|---|---|
| API key | From your Payzum merchant dashboard |
| Webhook secret | Verifies incoming payment webhooks (IPN) |
| Signature header | Legacy field kept for clean upgrades — the IPN controller always reads `x-nowpayments-sig`; anything typed here has no effect |

There is no settlement-currency setting: the plugin always sends
`pay_currency: "all"` and the buyer picks the coin on the hosted checkout.

## Order status mapping

| Payzum payment status | nopCommerce order |
|---|---|
| `finished` (amount and currency matching) | Marked paid (`MarkOrderAsPaidAsync`) |
| `finished` (amount/currency mismatch) | Not paid — acknowledged and left for manual reconciliation |
| `failed`, `expired` | Cancelled — unless the order is already paid, which a late terminal event never touches |
| `partially_paid` | No-op — the order stays pending while the buyer tops up; a later `finished` still marks it paid |

## FAQ

**Can a nopCommerce store accept USDT or USDC?**
Yes — with this plugin, buyers pay in USDC, USDT or other supported assets on
the chain they prefer, and the order is marked paid automatically.

**Is Payzum custodial?**
No. Funds settle directly to your own wallet — Payzum never holds your money.

**Do buyers need an account or a specific wallet?**
No. They scan a QR or copy a deposit address from the hosted checkout and pay
from any wallet.

**What about chargebacks?**
There are none — crypto payments are final, which eliminates chargeback fraud.

**Which nopCommerce versions are supported?**
4.70 (net8.0). The plugin follows the same project shape as the first-party
4.70 payment plugins.

**What data is shared with Payzum?**
Only the order total, currency, the order GUID and your store's callback
URL — no customer personal data. Endpoint: `https://merchant.payzum.com`.

## Related Payzum integrations

Payzum ships official plugins for most major e-commerce, donation and billing
platforms — WooCommerce, Magento 2, PrestaShop, Shopware 6, OpenCart, Zen Cart,
Ecwid, BigCommerce, Shopify, Wix, Medusa, Vendure, Saleor, Sylius, Easy Digital
Downloads, GiveWP, Paid Memberships Pro, WHMCS, Blesta, HostBill, ClientExec,
pretix, Frappe/ERPNext, Akaunting and django-payments — plus official SDKs for
PHP, Node.js/TypeScript, Python and Rust. Browse them all at
[github.com/payzum-dev](https://github.com/payzum-dev).

## About Payzum

[Payzum](https://payzum.com) is a non-custodial crypto payment gateway for
merchants: accept USDC, USDT and other digital assets with settlement straight
to your own wallet, optional auto-conversion to stablecoins, and a single REST
API. API docs: [merchant.payzum.com/api/docs](https://merchant.payzum.com/api/docs).

## License

[MIT](LICENSE). Contributed and maintained by Payzum.
