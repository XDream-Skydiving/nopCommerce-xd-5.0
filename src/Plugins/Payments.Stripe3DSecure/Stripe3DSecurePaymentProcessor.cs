using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.Stripe3DSecure.Components;
using Nop.Plugin.Payments.Stripe3DSecure.Services;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;
using SessionExtensions = Nop.Core.Http.Extensions.SessionExtensions;

namespace Nop.Plugin.Payments.Stripe3DSecure;

public class Stripe3DSecurePaymentProcessor : BasePlugin, IPaymentMethod, IPlugin, IWidgetPlugin
{
	protected readonly ILocalizationService _localizationService;

	protected readonly ILanguageService _languageService;

	protected readonly ISettingService _settingService;

	protected readonly IWebHelper _webHelper;

	protected readonly IStoreContext _storeContext;

	protected readonly Stripe3DSecurePaymentSettings _stripe3DSecurePaymentSettings;

	protected readonly Stripe3DSecureManager _stripe3DSecureManager;

	protected readonly IStripe3DSecureService _stripe3DSecureService;

	protected readonly IHttpContextAccessor _httpContextAccessor;

	protected readonly WidgetSettings _widgetSettings;

	protected readonly IPluginService _pluginService;

	protected readonly IOrderTotalCalculationService _orderTotalCalculationService;

	public bool SupportCapture => true;

	public bool SupportPartiallyRefund => true;

	public bool SupportRefund => true;

	public bool SupportVoid => true;

	public RecurringPaymentType RecurringPaymentType => (RecurringPaymentType)20;

	public PaymentMethodType PaymentMethodType => (PaymentMethodType)10;

	public bool SkipPaymentInfo => false;

	public bool HideInWidgetList => true;

	public Stripe3DSecurePaymentProcessor(ILocalizationService localizationService, ISettingService settingService, IWebHelper webHelper, IStoreContext storeContext, Stripe3DSecurePaymentSettings stripe3DSecurePaymentSettings, Stripe3DSecureManager stripe3DSecureManager, IStripe3DSecureService stripe3DSecureService, IHttpContextAccessor httpContextAccessor, WidgetSettings widgetSettings, IPluginService pluginService, IOrderTotalCalculationService orderTotalCalculationService, ILanguageService languageService)
	{
		_localizationService = localizationService;
		_languageService = languageService;
		_settingService = settingService;
		_webHelper = webHelper;
		_storeContext = storeContext;
		_stripe3DSecurePaymentSettings = stripe3DSecurePaymentSettings;
		_stripe3DSecureManager = stripe3DSecureManager;
		_stripe3DSecureService = stripe3DSecureService;
		_httpContextAccessor = httpContextAccessor;
		_widgetSettings = widgetSettings;
		_pluginService = pluginService;
		_orderTotalCalculationService = orderTotalCalculationService;
	}

	public Task<IList<string>> GetWidgetZonesAsync()
	{
		return Task.FromResult((IList<string>)new List<string>
		{
			PublicWidgetZones.OpCheckoutConfirmTop,
			PublicWidgetZones.CheckoutConfirmTop
		});
	}

	public Type GetWidgetViewComponent(string widgetZone)
	{
		return typeof(Stripe3DSecureOrderConfirmComponent);
	}

	public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
	{
		return await _stripe3DSecureManager.ProcessPaymentAsync(processPaymentRequest);
	}

	public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
	{
		await _stripe3DSecureManager.PostProcessPaymentAsync(postProcessPaymentRequest);
		_httpContextAccessor.HttpContext?.Session?.Remove(Stripe3DSecurePaymentDefaults.PaymentIntentKey);
		_httpContextAccessor.HttpContext?.Session?.Remove(Stripe3DSecurePaymentDefaults.PaymentMethodKey);
	}

	public async Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
	{
		if (_stripe3DSecurePaymentSettings == null || string.IsNullOrEmpty(_stripe3DSecurePaymentSettings.LicenseKey))
		{
			return true;
		}
		if (string.IsNullOrEmpty(_stripe3DSecurePaymentSettings.PublishableApiKey))
		{
			return true;
		}
		int num = await _stripe3DSecureService.CheckLicenseKeyValid(_stripe3DSecurePaymentSettings.LicenseKey);
		if (num != 100 && num != 101)
		{
			return true;
		}
		return false;
	}

	public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
	{
		return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart, _stripe3DSecurePaymentSettings.AdditionalFee, _stripe3DSecurePaymentSettings.AdditionalFeePercentage);
	}

	public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
	{
		return await _stripe3DSecureManager.CaptureAsync(capturePaymentRequest);
	}

	public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
	{
		return await _stripe3DSecureManager.RefundAsync(refundPaymentRequest);
	}

	public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
	{
		return await _stripe3DSecureManager.VoidAsync(voidPaymentRequest);
	}

	public async Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
	{
		return await _stripe3DSecureManager.ProcessRecurringPaymentAsync(processPaymentRequest);
	}

	public async Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
	{
		return await _stripe3DSecureManager.CancelRecurringPaymentAsync(cancelPaymentRequest);
	}

	public Task<bool> CanRePostProcessPaymentAsync(Order order)
	{
		ArgumentNullException.ThrowIfNull(order, "order");
		return Task.FromResult(result: false);
	}

	public async Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
	{
		List<string> warnings = new List<string>();
		if (form.TryGetValue("CardholderName", out var value) && StringValues.IsNullOrEmpty(value))
		{
			List<string> list = warnings;
			list.Add(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Payment.CardholderName.Required"));
		}
		if (form.TryGetValue("CardNonce", out var value2) && StringValues.IsNullOrEmpty(value2))
		{
			List<string> list = warnings;
			list.Add(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.CardNonce.Required"));
		}
		return warnings;
	}

	public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
	{
		ArgumentNullException.ThrowIfNull(form, "form");
		StringValues stringValues = form["CardNonce"];
		if (!StringValues.IsNullOrEmpty(stringValues))
		{
			await SessionExtensions.SetAsync<string>(_httpContextAccessor.HttpContext.Session, Stripe3DSecurePaymentDefaults.PaymentMethodKey, stringValues.FirstOrDefault());
		}
		return await Task.FromResult<ProcessPaymentRequest>(new ProcessPaymentRequest());
	}

	public override string GetConfigurationPageUrl()
	{
		return _webHelper.GetStoreLocation((bool?)null) + "Admin/Stripe3DSecure/MiscConfigure";
	}

	public Type GetPublicViewComponent()
	{
		PluginDescriptor result = _pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>("SevenSpikes.Nop.Plugins.RealOnePageCheckout", (LoadPluginsMode)10, (Customer)null, 0, (string)null).GetAwaiter().GetResult();
		if (result != null)
		{
			int result2 = _storeContext.GetActiveStoreScopeConfigurationAsync().GetAwaiter().GetResult();
			if (_settingService.GetSettingByKeyAsync<bool>("realonepagecheckoutsettings.enablerealonepagecheckout", false, result2, false).GetAwaiter().GetResult() && result.Installed)
			{
				return typeof(Stripe3DSecurePaymentInfoOpcComponent);
			}
		}
		return typeof(Stripe3DSecurePaymentInfoComponent);
	}

	public override async Task InstallAsync()
	{
        //TODO: This causes a hang on startup, need to investigate why.
        Stripe3DSecurePaymentSettings stripe3DSecurePaymentSettings = new Stripe3DSecurePaymentSettings
		{
			TransactMode = TransactMode.AuthorizeAndCapture
		};
		Stripe3DSecurePaymentSettings stripe3DSecurePaymentSettings2 = stripe3DSecurePaymentSettings;
		stripe3DSecurePaymentSettings2.StatementDescriptor = CommonHelper.EnsureMaximumLength((await _storeContext.GetCurrentStoreAsync()).Name, 22, null);
		stripe3DSecurePaymentSettings.SentReceiptEmail = true;
		stripe3DSecurePaymentSettings.PassBillingAddress = true;
		stripe3DSecurePaymentSettings.PassShippingAddress = true;
        //should this be setSettingAsync instead of SaveSettingAsync?  SaveSettingAsync will save to the database, but setSettingAsync will save to the cache.  I think this should be setSettingAsync.
        await _settingService.SaveSettingAsync<Stripe3DSecurePaymentSettings>(stripe3DSecurePaymentSettings);

		if (!_widgetSettings.ActiveWidgetSystemNames.Contains(Stripe3DSecurePaymentDefaults.PluginMethodSystemName))
		{
			_widgetSettings.ActiveWidgetSystemNames.Add(Stripe3DSecurePaymentDefaults.PluginMethodSystemName);
			await _settingService.SaveSettingAsync<WidgetSettings>(_widgetSettings, 0);
		}
		await _localizationService.AddOrUpdateLocaleResourceAsync(GetLocaleResources());
		await base.InstallAsync();
	}

	private static IDictionary<string, string> GetLocaleResources()
	{
		return new Dictionary<string, string>
		{
			["Plugin.Payments.Stripe3DSecure.Fields.Settings.Common.BlockTitle"] = "Common settings",
			["Plugin.Payments.Stripe3DSecure.Fields.Settings.Stripe.BlockTitle"] = "Stripe api settings",
			["Plugin.Payments.Stripe3DSecure.Fields.Settings.StripeWebhook.BlockTitle"] = "Stripe webhook settings",
			["Plugin.Payments.Stripe3DSecure.Fields.TransactMode"] = "Transaction mode",
			["Plugin.Payments.Stripe3DSecure.Fields.TransactMode.Hint"] = "Choose transaction mode.",
			["Plugin.Payments.Stripe3DSecure.Fields.AdditionalFee"] = "Additional fee",
			["Plugin.Payments.Stripe3DSecure.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
			["Plugin.Payments.Stripe3DSecure.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
			["Plugin.Payments.Stripe3DSecure.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
			["Plugin.Payments.Stripe3DSecure.Fields.PublishableApiKey"] = "Publishable key",
			["Plugin.Payments.Stripe3DSecure.Fields.PublishableApiKey.Hint"] = "Enter Publishable key",
			["Plugin.Payments.Stripe3DSecure.Fields.PublishableApiKey.Required"] = "Publishable key is required",
			["Plugin.Payments.Stripe3DSecure.Fields.SecretApiKey"] = "Secret key",
			["Plugin.Payments.Stripe3DSecure.Fields.SecretApiKey.Hint"] = "Enter Secret key",
			["Plugin.Payments.Stripe3DSecure.Fields.SecretApiKey.Required"] = "Secret key is required",
			["Plugin.Payments.Stripe3DSecure.Fields.StatementDescriptor"] = "Statement descriptor",
			["Plugin.Payments.Stripe3DSecure.Fields.StatementDescriptor.Hint"] = "A name that the browser shows the customer in the payment interface.",
			["Plugin.Payments.Stripe3DSecure.Fields.StatementDescriptor.Note"] = "Must be at most 22 characters(including white-space).",
			["Plugin.Payments.Stripe3DSecure.Fields.StatementDescriptor.MaxLengthValidation"] = "Must be at most less then or equal {0} characters(including white-space).",
			["Plugin.Payments.Stripe3DSecure.Fields.SentReceiptEmail"] = "Sent receipt email",
			["Plugin.Payments.Stripe3DSecure.Fields.SentReceiptEmail.Hint"] = "Stripe can automatically send email receipts after a successful payment.",
			["Plugin.Payments.Stripe3DSecure.Fields.PassBillingAddress"] = "Pass billing address",
			["Plugin.Payments.Stripe3DSecure.Fields.PassBillingAddress.Hint"] = "Sync billing address with strie payment detail.",
			["Plugin.Payments.Stripe3DSecure.Fields.PassShippingAddress"] = "Pass shipping address",
			["Plugin.Payments.Stripe3DSecure.Fields.PassShippingAddress.Hint"] = "Sync shipping address with strie payment detail.",
			["Plugin.Payments.Stripe3DSecure.Fields.WebhookUrl"] = "Webhook Url",
			["Plugin.Payments.Stripe3DSecure.Fields.WebhookUrl.Hint"] = "Configure Webhook Url to Stripe Account",
			["Plugin.Payments.Stripe3DSecure.Fields.WebhookId"] = "Webhook Id",
			["Plugin.Payments.Stripe3DSecure.Fields.WebhookId.Hint"] = "Stripe Webhook Id.",
			["Plugin.Payments.Stripe3DSecure.Fields.SigningSecretKey"] = "Webhook Signing Secret",
			["Plugin.Payments.Stripe3DSecure.Fields.SigningSecretKey.Hint"] = "Specify webhook 'Signing Secret' from webhook detail's page.",
			["Plugin.Payments.Stripe3DSecure.Fields.WebhookCreate"] = "Create Webhook",
			["Plugin.Payments.Stripe3DSecure.Fields.WebhookRemove"] = "Remove Webhook",
			["Plugin.Payments.Stripe3DSecure.Fields.Webhook.CreateSuccess"] = "Webhook created successfully.",
			["Plugin.Payments.Stripe3DSecure.Fields.Webhook.CreateError"] = "Webhook was not created (see details in the log)",
			["Plugin.Payments.Stripe3DSecure.Fields.Webhook.RemoveSuccess"] = "Webhook was removed successfully.",
			["Plugin.Payments.Stripe3DSecure.Fields.Webhook.RemoveError"] = "Webhook was not removed (see details in the log)",
			["Plugin.Payments.Stripe3DSecure.Fields.LicenseTitle"] = "Stripe 3DSecure License",
			["Plugin.Payments.Stripe3DSecure.Fields.LicenseKey"] = "License Key",
			["Plugin.Payments.Stripe3DSecure.Fields.LicenseKey.Hint"] = "Enter License Key Value",
			["Plugin.Payments.Stripe3DSecure.Fields.TrialMode"] = "Your Plugin Is In Trial Mode",
			["Plugin.Payments.Stripe3DSecure.Fields.DomainMismatch"] = "Your Domain Is Missmatched With This License Key",
			["Plugin.Payments.Stripe3DSecure.Fields.LicenseExpire"] = "Your stripe3D secure Plugin Is Expire",
			["Plugin.Payments.Stripe3DSecure.Fields.InvalidLicense"] = "Invalid license key. Please check your license key",
			["Plugin.Payments.Stripe3DSecure.Fields.EnterLicensekey"] = "Please enter your License key. If you don't have Licence key please contact to forefrontinfotech at info@forefrontinfotech.com",
			["Plugin.Payments.Stripe3DSecure.Fields.PaymentMethodDescription"] = "Pay by credit / debit card",
			["Plugin.Payments.Stripe3DSecure.Fields.CardNonce.Required"] = "Unable to retrive card detail to process payment.",
			["Plugin.Payments.Stripe3DSecure.Fields.StripeCustomer.Description"] = "Customer for an order guid {0}",
			["Plugin.Payments.Stripe3DSecure.Fields.StripePlan.Nickname"] = "Recurring plan for an order guid {0}",
			["Plugin.Payments.Stripe3DSecure.Fields.PaymentSuccess"] = "Your payment has been successfully processed",
			["Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Required"] = "Unable to retrive payment intent key to get payment detail from payment provider.",
			["Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Invalid"] = "Payment intent with invalid status to process payment.",
			["Plugin.Payments.Stripe3DSecure.Fields.Instructions"] = "\r\n                    <div class=\"bg-gray-light no-margin\" style=\"border-radius: .25rem;box-shadow: 0 1px 3px rgba(0,0,0,.12),0 1px 2px rgba(0,0,0,.24);padding: 15px 30px 15px 15px;border-left: 5px solid #e9ecef;margin-bottom: 1rem;\">\r\n                        <p>For plugin configuration follow these steps:</p>\r\n                        <ul style=\"list-style-type:none;\">\r\n                            <li>\r\n                                1. Sign up for a <a href=\"https://dashboard.stripe.com/register\" target=\"_blank\">Stripe Account</a>\r\n                            </li>\r\n                            <li>\r\n                                2. Upon stripe account approval an email will be sent to you with your Virtual Terminal credentials where you can confirm your email address.\r\n                            </li>\r\n                            <li>\r\n                                3. Steps to obtain each:\r\n                                <ul style=\"list-style-type:none;\">\r\n                                    <li>a. Sign into the Virtual Terminal with the login credentials that you received.</li>\r\n                                    <li>b. To obtain your Keys, navigate to the Developers tab and select the API Key link.</li>\r\n                                    <li>c. Select appropriate API keys – Publishable key and Secret key.</li>\r\n                                </ul>\r\n                            </li>\r\n                            <li>\r\n                                4. Fill in the remaining fields and save to complete the configuration\r\n                            </li>\r\n                            <li>\r\n                                5. Steps to Webhook features:\r\n                                <ul style=\"list-style-type:none;\">\r\n                                    <li>i. To configure your Webhook, navigate Developers tab and select the Webhooks link to the stripe webhook settings block.</li>\r\n                                    <li>ii. Click Create Webhook button to create webhook on your stripe account.</li>\r\n                                    <li>iii. Navigate to your stripe account Developers tab and select the Webhooks that we created from your store.</li>\r\n                                    <li>iv. Where you can check selected below events from the list of events in Webhook details block.</li>\r\n                                        <ul style=\"list-style-type:none;\">\r\n                                            <li>- payment_intent.succeeded </li>\r\n                                            <li>- payment_intent.payment_failed </li>\r\n                                            <li>- charge.refunded </li>\r\n                                            <li>- customer.subscription.created </li>\r\n                                            <li>- customer.subscription.deleted </li>\r\n                                        </ul>\r\n                                    <li>v. For field Webhook Signing Secret, After Webhook details block see the Signing secret block, Here click on 'Click to reveal' to retrive signing secret.</li>\r\n                                </ul>\r\n                            </li>\r\n                            <li>\r\n                                6. Save to complete the webhook configuration\r\n                            </li>\r\n                        </ul>\r\n                        <br /><b>Notes: </b>\r\n                        <ul style=\"list-style-type:none;\">\r\n                            <li>\r\n                                - <em>However, The payment form must be generated only on a webpage that uses HTTPS.</em>\r\n                            </li>\r\n                            <li>\r\n                                - <em>Some of the settings below are mandatory, if any is missing, the settings are invalid and the payment method is not shown to the customer on the checkout page.</em>\r\n                            </li>\r\n                            <li>\r\n                                - <em>The Stripe platform supports multiple currency; ensure that you have correctly configured the exchange rate from your primary store currency to the stripe supported currency. For more please contact to nopCommerceplus at '<b>info@nopCommerceplus.com</b>'.</em>\r\n                            </li>\r\n                        </ul>\r\n                        <br /><b><span style=\"color:red;\">Important notes: </span></b>\r\n                        <ul style=\"list-style-type:none;\">\r\n                            <li>\r\n                                - Using with <b>\"RealOnePageCheckoutPlugin\"</b> then need some changes on there configuration as bellow :\r\n                                <ul style=\"list-style-type:none;\">\r\n                                    <li>\r\n                                        - <em>In <b>Billing address settings</b> tab : there was field called <b>\"Saved billing address fields on change\"</b> here need to require fields <b>\"Email and CountryId\"</b>.</em>\r\n                                    </li>\r\n                                    <li>\r\n                                        - <em>In <b>Shipping address settings</b> tab : there was field called <b>\"Saved shipping address fields on change\"</b> here need to require fields <b>\"Email and CountryId\"</b>.</em>\r\n                                    </li>\r\n                                </ul>\r\n                            </li>\r\n                        </ul>\r\n                    </div>   \r\n                <br />",
			//Items below here weren't added
            ["Plugin.Payments.Stripe3DSecure.Update.Description"] = "{0} - Order #{1}",
			["Plugin.Payments.Stripe3DSecure.CustomerOptions.Description"] = "Customer for an store {0}",
			["Plugin.Payments.Stripe3DSecure.PaymentIntentUpdateOptions.Description"] = "{0} - Order #{1}",
			["Plugin.Payments.Stripe3DSecure.PartiallyMaximumRefundedAmount.Error"] = "This charge has already been partially refunded. Maximum refund amount is:",
			["Plugin.Payments.Stripe3DSecure.FullyRefundedCharge.Error"] = "This charge has already been fully refunded.",
			["Plugin.Payments.Stripe3DSecure.Payment.CardCode"] = "Card code",
			["Plugin.Payments.Stripe3DSecure.Payment.CardholderName"] = "Cardholder name",
			["Plugin.Payments.Stripe3DSecure.Payment.CardholderName.Required"] = "Enter cardholder name",
			["Plugin.Payments.Stripe3DSecure.Payment.CardNumber"] = "Card number",
			["Plugin.Payments.Stripe3DSecure.Payment.ExpirationDate"] = "Expiration date"
		};
	}

	public override async Task UpdateAsync(string currentVersion, string targetVersion)
	{
		if (!Version.TryParse(currentVersion, out var version))
			throw new FormatException($"Invalid Stripe3DSecure plugin version: {currentVersion}");

		if (version < new Version(1, 1))
		{
			var resources = GetLocaleResources();
			foreach (var language in await _languageService.GetAllLanguagesAsync(true))
			{
				var existingResources = await _localizationService.GetAllResourceValuesAsync(language.Id, null);
				var missingResources = resources
					.Where(resource => !existingResources.ContainsKey(resource.Key.Trim().ToLowerInvariant()))
					.ToDictionary(resource => resource.Key, resource => resource.Value);

				if (missingResources.Count > 0)
					await _localizationService.AddOrUpdateLocaleResourceAsync(missingResources, language.Id);
			}
		}

		await base.UpdateAsync(currentVersion, targetVersion);
	}

	public override async Task UninstallAsync()
	{
		await _stripe3DSecureManager.RemoveWebHookAsync();
		await _settingService.DeleteSettingAsync<Stripe3DSecurePaymentSettings>();
		if (_widgetSettings.ActiveWidgetSystemNames.Contains(Stripe3DSecurePaymentDefaults.PluginMethodSystemName))
		{
			_widgetSettings.ActiveWidgetSystemNames.Remove(Stripe3DSecurePaymentDefaults.PluginMethodSystemName);
			await _settingService.SaveSettingAsync<WidgetSettings>(_widgetSettings, 0);
		}
		await _localizationService.DeleteLocaleResourcesAsync("Plugin.Payments.Stripe3DSecure", (int?)null);
        await base.UninstallAsync();

    }

	public async Task<string> GetPaymentMethodDescriptionAsync()
	{
		return await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.PaymentMethodDescription");
	}

}
