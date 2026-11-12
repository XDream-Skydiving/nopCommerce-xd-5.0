using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Logging;
using Nop.Plugin.Payments.Stripe3DSecure.Models;
using Nop.Plugin.Payments.Stripe3DSecure.Services;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using ILogger = Nop.Services.Logging.ILogger;
using LogLevel = Nop.Core.Domain.Logging.LogLevel;

namespace Nop.Plugin.Payments.Stripe3DSecure.Controllers;

public class Stripe3DSecureController : BaseAdminController
{
	protected readonly ILocalizationService _localizationService;

	protected readonly ISettingService _settingService;

	protected readonly IWebHelper _webHelper;

	protected readonly IStoreContext _storeContext;

	protected readonly IStripe3DSecureService _stripe3DSecureService;

	protected readonly Stripe3DSecureManager _stripe3DSecureManager;

	protected readonly INotificationService _notificationService;

	protected readonly ILogger _logger;

	public Stripe3DSecureController(ILocalizationService localizationService, ISettingService settingService, IWebHelper webHelper, IStoreContext storeContext, IStripe3DSecureService stripe3DSecureService, Stripe3DSecureManager stripe3DSecureManager, INotificationService notificationService, ILogger logger)
	{
		_localizationService = localizationService;
		_settingService = settingService;
		_webHelper = webHelper;
		_storeContext = storeContext;
		_stripe3DSecureService = stripe3DSecureService;
		_stripe3DSecureManager = stripe3DSecureManager;
		_notificationService = notificationService;
		_logger = logger;
	}

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> MiscConfigure()
	{
		return ((Controller)(object)this).View("~/Plugins/Payments.Stripe3DSecure/Views/MiscConfigure.cshtml");
	}

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
	{
		int storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
		Stripe3DSecurePaymentSettings stripePaymentSettings = await _settingService.LoadSettingAsync<Stripe3DSecurePaymentSettings>(storeScope);
		if (string.IsNullOrEmpty(stripePaymentSettings.LicenseKey))
		{
			return ((ControllerBase)(object)this).RedirectToAction("LicenseKey");
		}
		try
		{
			switch (await _stripe3DSecureService.CheckLicenseKeyValid(stripePaymentSettings.LicenseKey))
			{
			case 100:
				break;
			case 101:
			{
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.TrialMode");
				break;
			}
			case 102:
			{
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.DomainMismatch");
				return ((ControllerBase)(object)this).RedirectToAction("LicenseKey");
			}
			case 103:
			{
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.LicenseExpire");
				return ((ControllerBase)(object)this).RedirectToAction("LicenseKey");
			}
			default:
			{
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.InvalidLicense");
				return ((ControllerBase)(object)this).RedirectToAction("LicenseKey");
			}
			}
		}
		catch (Exception ex)
		{
			await _logger.ErrorAsync(ex.Message, ex, (Customer)null);
			dynamic viewBag = ((Controller)this).ViewBag;
			viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.InvalidLicense");
			return ((ControllerBase)(object)this).RedirectToAction("LicenseKey");
		}
		ConfigurationModel model = new ConfigurationModel
		{
			ActiveStoreScopeConfiguration = storeScope,
			TransactModeId = Convert.ToInt32(stripePaymentSettings.TransactMode),
			AdditionalFee = stripePaymentSettings.AdditionalFee,
			AdditionalFeePercentage = stripePaymentSettings.AdditionalFeePercentage
		};
		model.TransactModeValues = (await stripePaymentSettings.TransactMode.ToSelectListAsync()).ToList();
		//model.TransactModeValues = (IList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>)await Extensions.ToSelectListAsync<TransactMode>(stripePaymentSettings.TransactMode, true, null, true);
		model.PublishableApiKey = stripePaymentSettings.PublishableApiKey;
		model.SecretApiKey = stripePaymentSettings.SecretApiKey;
		model.StatementDescriptor = stripePaymentSettings.StatementDescriptor;
		model.SentReceiptEmail = stripePaymentSettings.SentReceiptEmail;
		model.PassBillingAddress = stripePaymentSettings.PassBillingAddress;
		model.PassShippingAddress = stripePaymentSettings.PassShippingAddress;
		model.WebhookId = stripePaymentSettings.WebhookId;
		model.SigningSecretKey = stripePaymentSettings.SigningSecretKey;
		model.WebhookUrl = _webHelper.GetStoreLocation((bool?)null) + Stripe3DSecurePaymentDefaults.WebhookRouteUrl;
		if (storeScope > 0)
		{
			model.TransactModeId_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, TransactMode>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, TransactMode>>)((Stripe3DSecurePaymentSettings x) => x.TransactMode), storeScope);
			model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, decimal>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, decimal>>)((Stripe3DSecurePaymentSettings x) => x.AdditionalFee), storeScope);
			model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, bool>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, bool>>)((Stripe3DSecurePaymentSettings x) => x.AdditionalFeePercentage), storeScope);
			model.PublishableApiKey_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.PublishableApiKey), storeScope);
			model.SecretApiKey_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.SecretApiKey), storeScope);
			model.StatementDescriptor_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.StatementDescriptor), storeScope);
			model.SentReceiptEmail_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, bool>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, bool>>)((Stripe3DSecurePaymentSettings x) => x.SentReceiptEmail), storeScope);
			model.PassBillingAddress_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, bool>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, bool>>)((Stripe3DSecurePaymentSettings x) => x.PassBillingAddress), storeScope);
			model.PassShippingAddress_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, bool>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, bool>>)((Stripe3DSecurePaymentSettings x) => x.PassShippingAddress), storeScope);
			model.WebhookId_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.WebhookId), storeScope);
			model.SigningSecretKey_OverrideForStore = await _settingService.SettingExistsAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.SigningSecretKey), storeScope);
		}
		return ((Controller)(object)this).View("~/Plugins/Payments.Stripe3DSecure/Views/Configure.cshtml", model);
	}

	[HttpPost]
	[ActionName("Configure")]
	[FormValueRequired(new string[] { "save" })]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
	{
		if (!((ControllerBase)this).ModelState.IsValid)
		{
			return await Configure();
		}
		int storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
		Stripe3DSecurePaymentSettings stripePaymentSettings = await _settingService.LoadSettingAsync<Stripe3DSecurePaymentSettings>(storeScope);
		stripePaymentSettings.TransactMode = (TransactMode)model.TransactModeId;
		stripePaymentSettings.AdditionalFee = model.AdditionalFee;
		stripePaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
		stripePaymentSettings.PublishableApiKey = model.PublishableApiKey;
		stripePaymentSettings.SecretApiKey = model.SecretApiKey;
		stripePaymentSettings.StatementDescriptor = model.StatementDescriptor;
		stripePaymentSettings.SentReceiptEmail = model.SentReceiptEmail;
		stripePaymentSettings.PassBillingAddress = model.PassBillingAddress;
		stripePaymentSettings.PassShippingAddress = model.PassShippingAddress;
		stripePaymentSettings.WebhookId = model.WebhookId;
		stripePaymentSettings.SigningSecretKey = model.SigningSecretKey;
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, TransactMode>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, TransactMode>>)((Stripe3DSecurePaymentSettings x) => x.TransactMode), model.TransactModeId_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, decimal>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, decimal>>)((Stripe3DSecurePaymentSettings x) => x.AdditionalFee), model.AdditionalFee_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, bool>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, bool>>)((Stripe3DSecurePaymentSettings x) => x.AdditionalFeePercentage), model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.PublishableApiKey), model.PublishableApiKey_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.SecretApiKey), model.SecretApiKey_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.StatementDescriptor), model.StatementDescriptor_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, bool>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, bool>>)((Stripe3DSecurePaymentSettings x) => x.SentReceiptEmail), model.SentReceiptEmail_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, bool>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, bool>>)((Stripe3DSecurePaymentSettings x) => x.PassBillingAddress), model.PassBillingAddress_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, bool>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, bool>>)((Stripe3DSecurePaymentSettings x) => x.PassShippingAddress), model.PassShippingAddress_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.WebhookId), model.WebhookId_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.SigningSecretKey), model.SigningSecretKey_OverrideForStore, storeScope, false);
		await _settingService.ClearCacheAsync();
		INotificationService notificationService = _notificationService;
		notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"), true, 0);
		return await Configure();
	}

	[HttpPost]
	[ActionName("Configure")]
	[FormValueRequired(new string[] { "createwebhook" })]
	public async Task<IActionResult> GetWebhookId(ConfigurationModel model)
	{
		int storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
		Stripe3DSecurePaymentSettings stripePaymentSettings = await _settingService.LoadSettingAsync<Stripe3DSecurePaymentSettings>(storeScope);
		(stripePaymentSettings.WebhookId, stripePaymentSettings.SigningSecretKey) = await _stripe3DSecureManager.CreateWebHookAsync();
		await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.WebhookId), model.WebhookId_OverrideForStore, storeScope, true);
		if (!string.IsNullOrEmpty(stripePaymentSettings.WebhookId))
		{
			INotificationService notificationService = _notificationService;
			notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.Webhook.CreateSuccess"), true, 0);
		}
		else
		{
			INotificationService notificationService = _notificationService;
			notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.Webhook.CreateError"), true, 0);
		}
		return await Configure();
	}

	[HttpPost]
	[ActionName("Configure")]
	[FormValueRequired(new string[] { "removewebhook" })]
	public async Task<IActionResult> RemoveWebhook(ConfigurationModel model)
	{
		int storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
		Stripe3DSecurePaymentSettings stripePaymentSettings = await _settingService.LoadSettingAsync<Stripe3DSecurePaymentSettings>(storeScope);
		if (await _stripe3DSecureManager.RemoveWebHookAsync())
		{
			stripePaymentSettings.WebhookId = string.Empty;
			stripePaymentSettings.SigningSecretKey = string.Empty;
			await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.WebhookId), model.WebhookId_OverrideForStore, storeScope, true);
			await _settingService.SaveSettingOverridablePerStoreAsync<Stripe3DSecurePaymentSettings, string>(stripePaymentSettings, (Expression<Func<Stripe3DSecurePaymentSettings, string>>)((Stripe3DSecurePaymentSettings x) => x.SigningSecretKey), model.SigningSecretKey_OverrideForStore, storeScope, true);
		}
		if (!string.IsNullOrEmpty(stripePaymentSettings.WebhookId))
		{
			INotificationService notificationService = _notificationService;
			notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.Webhook.RemoveError"), true, 0);
		}
		else
		{
			INotificationService notificationService = _notificationService;
			notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.Webhook.RemoveSuccess"), true, 0);
		}
		return await Configure();
	}

	public async Task<IActionResult> LicenseKey(string licenseKey = "")
	{
		int num = await _storeContext.GetActiveStoreScopeConfigurationAsync();
		Stripe3DSecurePaymentSettings stripe3dSecurePaymentSettings = await _settingService.LoadSettingAsync<Stripe3DSecurePaymentSettings>(num);
		ConfigurationModel model = new ConfigurationModel
		{
			LicenseKey = stripe3dSecurePaymentSettings.LicenseKey
		};
		try
		{
			if (string.IsNullOrEmpty(licenseKey) && string.IsNullOrEmpty(model.LicenseKey))
			{
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.EnterLicensekey");
				return ((Controller)(object)this).View("~/Plugins/Payments.Stripe3DSecure/Views/LicenseKey.cshtml", (object?)model);
			}
			switch (string.IsNullOrEmpty(licenseKey) ? (await _stripe3DSecureService.CheckLicenseKeyValid(model.LicenseKey)) : (await _stripe3DSecureService.CheckLicenseKeyValid(licenseKey)))
			{
			case 100:
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				return ((ControllerBase)(object)this).RedirectToAction("Configure");
			case 101:
			{
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.TrialMode");
				return ((ControllerBase)(object)this).RedirectToAction("Configure");
			}
			case 102:
			{
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.DomainMismatch");
				break;
			}
			case 103:
			{
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.LicenseExpire");
				break;
			}
			default:
			{
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.InvalidLicense");
				break;
			}
			}
		}
		catch (Exception ex)
		{
			await _logger.InsertLogAsync((LogLevel)30, ex.Message.ToString(), ex.StackTrace.ToString(), (Customer)null);
			dynamic viewBag = ((Controller)this).ViewBag;
			viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.InvalidLicense");
		}
		return ((Controller)(object)this).View("~/Plugins/Payments.Stripe3DSecure/Views/LicenseKey.cshtml", (object?)model);
	}

	[HttpPost]
	[FormValueRequired(new string[] { "save" })]
	public async Task<IActionResult> LicenseKey(ConfigurationModel model)
	{
		int num = await _storeContext.GetActiveStoreScopeConfigurationAsync();
		Stripe3DSecurePaymentSettings stripe3dSecurePaymentSettings = await _settingService.LoadSettingAsync<Stripe3DSecurePaymentSettings>(num);
		stripe3dSecurePaymentSettings.LicenseKey = model.LicenseKey;
		try
		{
			switch (await _stripe3DSecureService.CheckLicenseKeyValid(model.LicenseKey))
			{
			case 100:
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				return ((ControllerBase)(object)this).RedirectToAction("Configure");
			case 101:
			{
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.TrialMode");
				return ((ControllerBase)(object)this).RedirectToAction("Configure");
			}
			case 102:
			{
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.DomainMismatch");
				return await LicenseKey();
			}
			case 103:
			{
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.LicenseExpire");
				return await LicenseKey();
			}
			default:
			{
				await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3dSecurePaymentSettings, 0);
				dynamic viewBag = ((Controller)this).ViewBag;
				viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.InvalidLicense");
				return await LicenseKey(model.LicenseKey);
			}
			}
		}
		catch (Exception ex)
		{
			await _logger.InsertLogAsync((LogLevel)30, ex.Message.ToString(), ex.StackTrace.ToString(), (Customer)null);
			dynamic viewBag = ((Controller)this).ViewBag;
			viewBag.Warning = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.InvalidLicense");
			return ((Controller)(object)this).View("~/Plugins/Payments.Stripe3DSecure/Views/LicenseKey.cshtml", (object?)model);
		}
	}
}
