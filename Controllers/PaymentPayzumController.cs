using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.Payzum.Models;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Payzum.Controllers;

/// <summary>Admin configuration screen for the Payzum payment method.</summary>
[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class PaymentPayzumController : BasePaymentController
{
    private readonly ISettingService _settingService;
    private readonly IPermissionService _permissionService;
    private readonly INotificationService _notificationService;
    private readonly PayzumPaymentSettings _settings;

    public PaymentPayzumController(
        ISettingService settingService,
        IPermissionService permissionService,
        INotificationService notificationService,
        PayzumPaymentSettings settings)
    {
        _settingService = settingService;
        _permissionService = permissionService;
        _notificationService = notificationService;
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Configure()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
            return AccessDeniedView();

        var model = new ConfigurationModel
        {
            ApiKey = _settings.ApiKey,
            WebhookSecret = _settings.WebhookSecret,
            SignatureHeader = _settings.SignatureHeader,
            IpnUrl = $"{Request.Scheme}://{Request.Host}/Plugins/PaymentPayzum/Ipn"
        };

        return View("~/Plugins/Payments.Payzum/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
            return AccessDeniedView();

        if (!ModelState.IsValid)
            return await Configure();

        _settings.ApiKey = model.ApiKey?.Trim() ?? string.Empty;
        _settings.WebhookSecret = model.WebhookSecret?.Trim() ?? string.Empty;
        _settings.SignatureHeader = string.IsNullOrWhiteSpace(model.SignatureHeader) ? "x-nowpayments-sig" : model.SignatureHeader.Trim();

        await _settingService.SaveSettingAsync(_settings);
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification("Payzum settings saved.");

        return await Configure();
    }
}
