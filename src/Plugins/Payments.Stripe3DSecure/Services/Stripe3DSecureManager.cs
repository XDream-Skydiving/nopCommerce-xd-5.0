using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Stores;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.Stripe3DSecure.Domain;
using Nop.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Stripe;
using ILogger = Nop.Services.Logging.ILogger;
using LogLevel = Nop.Core.Domain.Logging.LogLevel;
using SessionExtensions = Nop.Core.Http.Extensions.SessionExtensions;

namespace Nop.Plugin.Payments.Stripe3DSecure.Services;

public class Stripe3DSecureManager
{
	protected readonly ILogger _logger;

	protected readonly IWebHelper _webHelper;

	protected readonly Stripe3DSecurePaymentSettings _stripe3DSecurePaymentSettings;

	protected readonly ILocalizationService _localizationService;

	protected readonly IWorkContext _workContext;

	protected readonly IStoreContext _storeContext;

	protected readonly IOrderProcessingService _orderProcessingService;

	protected readonly ICurrencyService _currencyService;

	protected readonly IGenericAttributeService _genericAttributeService;

	protected readonly CurrencySettings _currencySettings;

	protected readonly IPriceCalculationService _priceCalculationService;

	protected readonly IShoppingCartService _shoppingCartService;

	protected readonly IOrderService _orderService;

	protected readonly IAddressService _addressService;

	protected readonly IProductService _productService;

	protected readonly IHttpContextAccessor _httpContextAccessor;

	protected readonly IStoreService _storeService;

	public Stripe3DSecureManager(ILogger logger, IWebHelper webHelper, Stripe3DSecurePaymentSettings stripe3DSecurePaymentSettings, ILocalizationService localizationService, IWorkContext workContext, IStoreContext storeContext, IOrderProcessingService orderProcessingService, ICurrencyService currencyService, IGenericAttributeService genericAttributeService, CurrencySettings currencySettings, IPriceCalculationService priceCalculationService, IShoppingCartService shoppingCartService, IOrderService orderService, IAddressService addressService, IProductService productService, IHttpContextAccessor httpContextAccessor, IStoreService storeService)
	{
		_logger = logger;
		_webHelper = webHelper;
		_stripe3DSecurePaymentSettings = stripe3DSecurePaymentSettings;
		_localizationService = localizationService;
		_workContext = workContext;
		_storeContext = storeContext;
		_orderProcessingService = orderProcessingService;
		_currencyService = currencyService;
		_genericAttributeService = genericAttributeService;
		_currencySettings = currencySettings;
		_priceCalculationService = priceCalculationService;
		_shoppingCartService = shoppingCartService;
		_orderService = orderService;
		_addressService = addressService;
		_productService = productService;
		_httpContextAccessor = httpContextAccessor;
		_storeService = storeService;
		StripeConfiguration.ApiKey = stripe3DSecurePaymentSettings.SecretApiKey;
		StripeConfiguration.AppInfo = new AppInfo
		{
			Name = "Card Payment(3DSecure) - nopCommercePlus(Forefront Infotech)",
			Version = "4.00",
			Url = "http://www.nopcommerceplus.com/stripe-3d-secure"
		};
	}

	private async Task<Stripe3DSecurePaymentResponce> CreatePaymentIntentAsync(Stripe3DSecurePaymentIntentRequest paymentIntentRequest)
	{
		Stripe3DSecurePaymentResponce paymentResponce = new Stripe3DSecurePaymentResponce();
		string text = ((!string.IsNullOrEmpty(_stripe3DSecurePaymentSettings.StatementDescriptor)) ? _stripe3DSecurePaymentSettings.StatementDescriptor : "nopCommerce store");
		try
		{
			PaymentIntentService paymentIntentService = new PaymentIntentService();
			PaymentIntentCreateOptions intentCreateOptions = new PaymentIntentCreateOptions
			{
				Amount = paymentIntentRequest.OrderTotal,
				Currency = paymentIntentRequest.CurrencyCode,
				AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
				{
					Enabled = true,
					AllowRedirects = "never"
				},
				StatementDescriptorSuffix = CommonHelper.EnsureMaximumLength(text, 22, (string)null),
				Metadata = new Dictionary<string, string> { 
				{
					"OrderGuid",
					paymentIntentRequest.OrderGuid.ToString()
				} },
				CaptureMethod = paymentIntentRequest.CaptureMethod
			};
			if (paymentIntentRequest.IsRecurring)
			{
				var customer = await _workContext.GetCurrentCustomerAsync();
				Store store = await _storeContext.GetCurrentStoreAsync();
				string text2 = await _genericAttributeService.GetAttributeAsync<string>((BaseEntity)(object)customer, Stripe3DSecurePaymentDefaults.StripeCustomerKey, ((BaseEntity)store).Id, (string)null);
				if (string.IsNullOrEmpty(text2))
				{
                    var val = await _addressService.GetAddressByIdAsync(customer.BillingAddressId.GetValueOrDefault());
					string email = ((customer != null) ? customer.Email : null) ?? ((val != null) ? val.Email : null);
					CustomerCreateOptions customerCreateOptions = new CustomerCreateOptions
					{
						Email = email
					};
					CustomerCreateOptions customerCreateOptions2 = customerCreateOptions;
					customerCreateOptions2.Description = string.Format(string.Format(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.CustomerOptions.Description"), store.Name), default(ReadOnlySpan<object>));
                    var customer2 = new CustomerService().Create(customerCreateOptions);
					intentCreateOptions.Customer = customer2.Id;
					await _genericAttributeService.SaveAttributeAsync<string>((BaseEntity)(object)customer, Stripe3DSecurePaymentDefaults.StripeCustomerKey, customer2.Id, ((BaseEntity)store).Id);
				}
				else
				{
					intentCreateOptions.Customer = text2;
				}
			}
			PaymentIntent paymentIntent = paymentIntentService.Create(intentCreateOptions);
			paymentResponce.PaymentIntentId = paymentIntent.Id;
			paymentResponce.ClientSecret = paymentIntent.ClientSecret;
		}
		catch (StripeException ex)
		{
			if (string.IsNullOrEmpty(ex.StripeError.Code))
			{
				await _logger.ErrorAsync(ex.StripeError.Message, (Exception)null, null);
			}
			else
			{
				await _logger.ErrorAsync(ex.StripeError.Message + " Error code: " + ex.StripeError.Code, (Exception)null, null);
			}
			paymentResponce.Warnings.Add(ex.StripeError.Message);
		}
		return paymentResponce;
	}

	private async Task<PaymentIntent> UpdatePaymentIntentAsync(string paymentIntentId, string orderId, int storeId)
	{
		PaymentIntent paymentIntent = null;
		try
		{
			if (!string.IsNullOrEmpty(paymentIntentId))
			{
				PaymentIntentService paymentIntentService = new PaymentIntentService();
				PaymentIntentUpdateOptions paymentIntentUpdateOptions = new PaymentIntentUpdateOptions();
				PaymentIntentUpdateOptions paymentIntentUpdateOptions2 = paymentIntentUpdateOptions;
				paymentIntentUpdateOptions2.Description = string.Format(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Update.Description"), (await _storeService.GetStoreByIdAsync(storeId)).Name, orderId);
				paymentIntent = await paymentIntentService.UpdateAsync(paymentIntentId, paymentIntentUpdateOptions);
			}
		}
		catch (StripeException ex)
		{
			if (string.IsNullOrEmpty(ex.StripeError?.Code))
			{
				await _logger.InsertLogAsync((LogLevel)40, $"Card Payment(3DSecure) - {ex.StripeError.Message}", ex.StackTrace, null);
			}
			else
			{
				await _logger.InsertLogAsync((LogLevel)40, $"Card Payment(3DSecure) - {ex.StripeError.Message} Error code : {ex.StripeError.Code}", ex.StackTrace, null);
			}
		}
		return paymentIntent;
	}

	public async Task<int> ConvertToZeroDecimal(decimal amount, string currentCurrency)
	{
		if (Array.Exists(Stripe3DSecurePaymentDefaults.ZeroDecimal, (string z) => z == currentCurrency.ToUpper()))
		{
			return (int)Math.Ceiling(await _priceCalculationService.RoundPriceAsync(amount, (Currency)null));
		}
		return (int)Math.Ceiling(await _priceCalculationService.RoundPriceAsync(amount, (Currency)null) * 100m);
	}

	public async Task<(string, string)> CreateWebHookAsync()
	{
		try
		{
			WebhookEndpointService webhookEndpointService = new WebhookEndpointService();
			if (!string.IsNullOrEmpty(_stripe3DSecurePaymentSettings.WebhookId))
			{
				try
				{
					if (webhookEndpointService.Get(_stripe3DSecurePaymentSettings.WebhookId) != null)
					{
						webhookEndpointService.Delete(_stripe3DSecurePaymentSettings.WebhookId);
					}
				}
				catch (StripeException ex)
				{
					await _logger.ErrorAsync($"Card Payment(3DSecure) error: failure message {ex.StripeError}", ex.InnerException, null);
				}
			}
			string storeLocation = _webHelper.GetStoreLocation((bool?)null);
			WebhookEndpointCreateOptions options = new WebhookEndpointCreateOptions
			{
				Url = storeLocation + Stripe3DSecurePaymentDefaults.WebhookRouteUrl,
				EnabledEvents = Stripe3DSecurePaymentDefaults.WebhookEventNames,
				ApiVersion = StripeConfiguration.ApiVersion
			};
			WebhookEndpoint webhookEndpoint = webhookEndpointService.Create(options);
			return (webhookEndpoint.Id, webhookEndpoint.Secret);
		}
		catch (StripeException ex2)
		{
			await _logger.ErrorAsync($"Card Payment(3DSecure) error: failure message {ex2.StripeError}", ex2.InnerException, null);
		}
		return (string.Empty, string.Empty);
	}

	public async Task<bool> RemoveWebHookAsync()
	{
		if (string.IsNullOrEmpty(_stripe3DSecurePaymentSettings.WebhookId))
		{
			return true;
		}
		try
		{
			WebhookEndpoint webhookEndpoint = new WebhookEndpointService().Delete(_stripe3DSecurePaymentSettings.WebhookId);
			return webhookEndpoint == null || (webhookEndpoint.Deleted.HasValue && webhookEndpoint.Deleted.Value);
		}
		catch (StripeException ex)
		{
			await _logger.ErrorAsync($"Card Payment(3DSecure) error: failure message {ex.StripeError}", ex.InnerException, null);
			return false;
		}
	}

	public async Task SubscriptionCancelAtPeriodEndAsync(string subscriptionTransactionId)
	{
		try
		{
			new SubscriptionService().Update(subscriptionTransactionId, new SubscriptionUpdateOptions
			{
				CancelAtPeriodEnd = true
			});
		}
		catch (StripeException ex)
		{
			await _logger.ErrorAsync($"Card Payment(3DSecure) error: failure message {ex.StripeError}", (Exception)ex, null);
		}
	}

	public async Task<Stripe3DSecurePaymentResponce> CreatePaymentIntentSecretAsync(Stripe3DSecurePaymentIntentRequest paymentIntentRequest)
	{
		Stripe3DSecurePaymentResponce paymentIntentResponce = new Stripe3DSecurePaymentResponce();
		try
		{
			PaymentIntentService paymentIntentService = new PaymentIntentService();
			PaymentIntent paymentIntent = null;
			if (!string.IsNullOrWhiteSpace(paymentIntentRequest.PaymentIntentId))
			{
				paymentIntent = paymentIntentService.Get(paymentIntentRequest.PaymentIntentId);
			}
			if (paymentIntent == null)
			{
				paymentIntentResponce = await CreatePaymentIntentAsync(paymentIntentRequest);
			}
			else if (paymentIntent.Status == Stripe3DSecurePaymentDefaults.PaymentIntentStatusRequiresAction || paymentIntent.Status == Stripe3DSecurePaymentDefaults.PaymentIntentStatusSucceeded || paymentIntent.Status == Stripe3DSecurePaymentDefaults.PaymentIntentStatusRequiresCapture || paymentIntent.Amount != paymentIntentRequest.OrderTotal)
			{
				paymentIntentService.Cancel(paymentIntent.Id, new PaymentIntentCancelOptions
				{
					CancellationReason = "duplicate"
				});
				paymentIntentResponce = await CreatePaymentIntentAsync(paymentIntentRequest);
			}
			else if ((paymentIntent.Status == Stripe3DSecurePaymentDefaults.PaymentIntentStatusRequiresConfirmation || paymentIntent.Status == Stripe3DSecurePaymentDefaults.PaymentIntentStatusRequiresPaymentMethod) && paymentIntent.Amount == paymentIntentRequest.OrderTotal)
			{
				paymentIntentResponce.PaymentIntentId = paymentIntent.Id;
				paymentIntentResponce.ClientSecret = paymentIntent.ClientSecret;
			}
			else
			{
				PaymentIntentUpdateOptions options = new PaymentIntentUpdateOptions
				{
					Amount = paymentIntentRequest.OrderTotal
				};
				paymentIntent = paymentIntentService.Update(paymentIntent.Id, options);
				paymentIntentResponce.PaymentIntentId = paymentIntent.Id;
				paymentIntentResponce.ClientSecret = paymentIntent.ClientSecret;
			}
			if (paymentIntentResponce.Warnings.Any())
			{
				return paymentIntentResponce;
			}
			if (string.IsNullOrEmpty(paymentIntentResponce.ClientSecret))
			{
				throw new NopException("Unable to retrive payment intent client secret");
			}
		}
		catch (StripeException ex)
		{
			if (!string.IsNullOrEmpty(ex.StripeError.Code))
			{
				paymentIntentResponce.Warnings.Add(ex.StripeError.Message + " Error code: " + ex.StripeError.Code);
			}
			else
			{
				paymentIntentResponce.Warnings.Add(ex.StripeError.Message);
			}
		}
		return paymentIntentResponce;
	}

	public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
	{
		ProcessPaymentResult result = new ProcessPaymentResult
		{
			NewPaymentStatus = (PaymentStatus)10
		};
		string paymentIntentKey = await SessionExtensions.GetAsync<string>(_httpContextAccessor.HttpContext.Session, Stripe3DSecurePaymentDefaults.PaymentIntentKey);
		if (string.IsNullOrEmpty(paymentIntentKey))
		{
			ProcessPaymentResult val = result;
			((BaseNopResult)val).AddError(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Required"));
		}
		try
		{
			PaymentIntentService paymentIntentService = new PaymentIntentService();
			PaymentIntent paymentIntent = paymentIntentService.Get(paymentIntentKey);
			if (paymentIntent == null)
			{
				ProcessPaymentResult val = result;
				((BaseNopResult)val).AddError(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Required"));
			}
			if (paymentIntent.Status != Stripe3DSecurePaymentDefaults.PaymentIntentStatusSucceeded && paymentIntent.Status != Stripe3DSecurePaymentDefaults.PaymentIntentStatusRequiresCapture)
			{
				ProcessPaymentResult val = result;
				((BaseNopResult)val).AddError(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Invalid"));
			}
			if (((BaseNopResult)result).Errors.Any())
			{
				return result;
			}
			if (paymentIntent.Metadata.Any())
			{
				if (Guid.TryParse(paymentIntent.Metadata["OrderGuid"], out var result2))
				{
					processPaymentRequest.OrderGuid = result2;
				}
				else
				{
					PaymentIntentUpdateOptions options = new PaymentIntentUpdateOptions
					{
						Metadata = new Dictionary<string, string> { 
						{
							"OrderGuid",
							processPaymentRequest.OrderGuid.ToString()
						} }
					};
					paymentIntent = paymentIntentService.Update(paymentIntent.Id, options);
				}
			}
			else
			{
				PaymentIntentUpdateOptions options2 = new PaymentIntentUpdateOptions
				{
					Metadata = new Dictionary<string, string> { 
					{
						"OrderGuid",
						processPaymentRequest.OrderGuid.ToString()
					} }
				};
				paymentIntent = paymentIntentService.Update(paymentIntent.Id, options2);
			}
			result.AuthorizationTransactionResult = JsonConvert.SerializeObject((object)paymentIntent);
			result.AuthorizationTransactionId = paymentIntent.Id;
			result.NewPaymentStatus = (PaymentStatus)20;
		}
		catch (StripeException ex)
		{
			if (!string.IsNullOrEmpty(ex.StripeError?.Code))
			{
				((BaseNopResult)result).AddError($"{ex.StripeError.Message} Error code: {ex.StripeError.Code}");
			}
			else
			{
				((BaseNopResult)result).AddError(ex.StripeError.Message);
			}
		}
		return result;
	}

	public virtual async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
	{
		Order order = postProcessPaymentRequest.Order;
		try
		{
			string text = await SessionExtensions.GetAsync<string>(_httpContextAccessor.HttpContext.Session, Stripe3DSecurePaymentDefaults.PaymentIntentKey);
			if (!string.IsNullOrEmpty(text))
			{
				PaymentIntent paymentIntent = await UpdatePaymentIntentAsync(text, order.CustomOrderNumber, order.StoreId);
				if (paymentIntent == null)
				{
					return;
				}
				if ((int)order.PaymentStatus != 20)
				{
					return;
				}
				if (_stripe3DSecurePaymentSettings.TransactMode == TransactMode.AuthorizeAndCapture)
				{
					paymentIntent = new PaymentIntentService().Capture(paymentIntent.Id, new PaymentIntentCaptureOptions());
					order.CaptureTransactionId = paymentIntent.LatestChargeId;
					order.CaptureTransactionResult = paymentIntent.RawJsonElement?.GetRawText();
					await _orderService.UpdateOrderAsync(order);
					if (_orderProcessingService.CanMarkOrderAsPaid(order))
					{
						await _orderProcessingService.MarkOrderAsPaidAsync(order);
					}
				}
				if (!string.IsNullOrEmpty(order.SubscriptionTransactionId))
				{
					RecurringPayment recurringPayment = ((IEnumerable<RecurringPayment>)(await _orderService.SearchRecurringPaymentsAsync(0, 0, ((BaseEntity)order).Id, null, 0, int.MaxValue, false))).FirstOrDefault();
					if (recurringPayment != null && !(await _orderService.GetRecurringPaymentHistoryAsync(recurringPayment)).Any())
					{
						await _orderService.InsertRecurringPaymentHistoryAsync(new RecurringPaymentHistory
						{
							RecurringPaymentId = ((BaseEntity)recurringPayment).Id,
							OrderId = ((BaseEntity)order).Id,
							CreatedOnUtc = DateTime.UtcNow
						});
					}
				}
				return;
			}
			ILogger logger = _logger;
			await logger.InsertLogAsync((LogLevel)40, string.Format("Card Payment(3DSecure) - {0}", await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Required")), "", null);
		}
		catch (StripeException ex)
		{
			if (string.IsNullOrEmpty(ex.StripeError?.Code))
			{
				await _logger.InsertLogAsync((LogLevel)40, $"Card Payment(3DSecure) - {ex.StripeError.Message}", ex.StackTrace, null);
			}
			else
			{
				await _logger.InsertLogAsync((LogLevel)40, $"Card Payment(3DSecure) - {ex.StripeError.Message} Error code : {ex.StripeError.Code}", ex.StackTrace, null);
			}
		}
	}

	public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
	{
		CapturePaymentResult result = new CapturePaymentResult();
		try
		{
			PaymentIntent paymentIntent = new PaymentIntentService().Capture(capturePaymentRequest.Order.AuthorizationTransactionId, new PaymentIntentCaptureOptions());
			string latestChargeId = paymentIntent.LatestChargeId;
			if (!string.IsNullOrEmpty(latestChargeId))
			{
				result.NewPaymentStatus = (PaymentStatus)30;
				result.CaptureTransactionResult = paymentIntent.RawJsonElement?.GetRawText();
				result.CaptureTransactionId = latestChargeId;
			}
		}
		catch (StripeException ex)
		{
			if (!string.IsNullOrEmpty(ex.StripeError?.Code))
			{
				((BaseNopResult)result).AddError($"{ex.StripeError.Message} Error code: {ex.StripeError.Code}");
			}
			else
			{
				((BaseNopResult)result).AddError(ex.StripeError.Message);
			}
			await _logger.InsertLogAsync((LogLevel)40, $"Card Payment(3DSecure) - {ex.StripeError.Message}", ex.StackTrace, null);
		}
		return result;
	}

	public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
	{
		RefundPaymentResult result = new RefundPaymentResult();
		try
		{
			Charge charge = new ChargeService().Get(refundPaymentRequest.Order.CaptureTransactionId);
			long maximumRefund = charge.Amount - charge.AmountRefunded;
			Currency val = await _currencyService.GetCurrencyByCodeAsync(refundPaymentRequest.Order.CustomerCurrencyCode);
			int num = ((!Array.Exists(Stripe3DSecurePaymentDefaults.ZeroDecimal, (string z) => z.ToLower() == refundPaymentRequest.Order.CustomerCurrencyCode.ToLower())) ? ((int)Math.Ceiling(Math.Round(await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(refundPaymentRequest.AmountToRefund, val), 2) * 100m)) : ((int)Math.Ceiling(Math.Round(await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(refundPaymentRequest.AmountToRefund, val), 2))));
			if (maximumRefund == 0L && !refundPaymentRequest.IsPartialRefund)
			{
				result.NewPaymentStatus = (PaymentStatus)40;
			}
			else if (num > maximumRefund)
			{
				if (maximumRefund > 0)
				{
					RefundPaymentResult val2 = result;
					((BaseNopResult)val2).AddError(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.PartiallyMaximumRefundedAmount.Error") + (decimal)maximumRefund / 100m);
				}
				else
				{
					RefundPaymentResult val2 = result;
					((BaseNopResult)val2).AddError(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.FullyRefundedCharge.Error"));
				}
			}
			else
			{
				RefundCreateOptions options = new RefundCreateOptions
				{
					Charge = charge.Id,
					Amount = num,
					Metadata = new Dictionary<string, string> { 
					{
						"OrderGuid",
						refundPaymentRequest.Order.OrderGuid.ToString()
					} }
				};
				new RefundService().Create(options);
				refundPaymentRequest.IsPartialRefund = num != maximumRefund;
				result.NewPaymentStatus = (PaymentStatus)(refundPaymentRequest.IsPartialRefund ? 35 : 40);
			}
		}
		catch (StripeException ex)
		{
			if (!string.IsNullOrEmpty(ex.StripeError?.Code))
			{
				((BaseNopResult)result).AddError($"{ex.StripeError.Message} Error code: {ex.StripeError.Code}");
			}
			else
			{
				((BaseNopResult)result).AddError(ex.StripeError.Message);
			}
		}
		return result;
	}

	public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
	{
		VoidPaymentResult result = new VoidPaymentResult();
		try
		{
			new PaymentIntentService().Cancel(voidPaymentRequest.Order.AuthorizationTransactionId, new PaymentIntentCancelOptions());
			result.NewPaymentStatus = (PaymentStatus)50;
		}
		catch (StripeException ex)
		{
			if (!string.IsNullOrEmpty(ex.StripeError?.Code))
			{
				((BaseNopResult)result).AddError($"{ex.StripeError.Message} Error code: {ex.StripeError.Code}");
			}
			else
			{
				((BaseNopResult)result).AddError(ex.StripeError.Message);
			}
			await _logger.InsertLogAsync((LogLevel)40, $"Card Payment(3DSecure) - {ex.StripeError.Message}", ex.StackTrace, null);
		}
		return result;
	}

	public async Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
	{
		ProcessPaymentResult result = new ProcessPaymentResult
		{
			NewPaymentStatus = (PaymentStatus)10
		};
		string paymentIntentKey = await SessionExtensions.GetAsync<string>(_httpContextAccessor.HttpContext.Session, Stripe3DSecurePaymentDefaults.PaymentIntentKey);
		if (string.IsNullOrEmpty(paymentIntentKey))
		{
			ProcessPaymentResult val = result;
			((BaseNopResult)val).AddError(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Required"));
		}
		try
		{
			PaymentIntentService paymentIntentService = new PaymentIntentService();
			PaymentIntent paymentIntent = paymentIntentService.Get(paymentIntentKey);
			if (paymentIntent == null)
			{
				ProcessPaymentResult val = result;
				((BaseNopResult)val).AddError(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Required"));
			}
			if (paymentIntent.Status != Stripe3DSecurePaymentDefaults.PaymentIntentStatusSucceeded && paymentIntent.Status != Stripe3DSecurePaymentDefaults.PaymentIntentStatusRequiresCapture)
			{
				ProcessPaymentResult val = result;
				((BaseNopResult)val).AddError(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.PaymentIntentId.Invalid"));
			}
			if (((BaseNopResult)result).Errors.Any())
			{
				return result;
			}
			if (paymentIntent.Metadata.Count != 0)
			{
				if (Guid.TryParse(paymentIntent.Metadata["OrderGuid"], out var result2))
				{
					processPaymentRequest.OrderGuid = result2;
				}
				else
				{
					PaymentIntentUpdateOptions options = new PaymentIntentUpdateOptions
					{
						Metadata = new Dictionary<string, string> { 
						{
							"OrderGuid",
							processPaymentRequest.OrderGuid.ToString()
						} }
					};
					paymentIntent = paymentIntentService.Update(paymentIntent.Id, options);
				}
			}
			else
			{
				PaymentIntentUpdateOptions options2 = new PaymentIntentUpdateOptions
				{
					Metadata = new Dictionary<string, string> { 
					{
						"OrderGuid",
						processPaymentRequest.OrderGuid.ToString()
					} }
				};
				paymentIntent = paymentIntentService.Update(paymentIntent.Id, options2);
			}
			IShoppingCartService shoppingCartService = _shoppingCartService;
			ShoppingCartItem val2 = (await shoppingCartService.GetShoppingCartAsync(await _workContext.GetCurrentCustomerAsync(), (ShoppingCartType)1, processPaymentRequest.StoreId, (int?)null, (DateTime?)null, (DateTime?)null, (int?)null)).FirstOrDefault();
			var product = await _productService.GetProductByIdAsync((val2 != null) ? val2.ProductId : 0);
			string stripeProductId = await _genericAttributeService.GetAttributeAsync<string>((BaseEntity)(object)product, Stripe3DSecurePaymentDefaults.StripeProductKey, 0, (string)null);
			if (string.IsNullOrEmpty(stripeProductId))
			{
				ProductCreateOptions productCreateOptions = new ProductCreateOptions();
				object name;
				if (string.IsNullOrEmpty((product != null) ? product.Name : null))
				{
					name = processPaymentRequest.OrderGuid.ToString();
				}
				else
				{
					name = ((product != null) ? product.Name : null);
				}
				productCreateOptions.Name = (string)name;
				productCreateOptions.Type = "service";
				productCreateOptions.Description = ((product != null) ? product.ShortDescription : null);
				ProductCreateOptions options3 = productCreateOptions;
				var product2 = new Stripe.ProductService().Create(options3);
				stripeProductId = product2.Id;
				await _genericAttributeService.SaveAttributeAsync<string>((BaseEntity)(object)product, Stripe3DSecurePaymentDefaults.StripeProductKey, product2.Id, 0);
			}
			ICurrencyService currencyService = _currencyService;
			IGenericAttributeService genericAttributeService = _genericAttributeService;
			Currency val3 = await currencyService.GetCurrencyByIdAsync(await genericAttributeService.GetAttributeAsync<int>((BaseEntity)(object)(await _workContext.GetCurrentCustomerAsync()), "CurrencyId", processPaymentRequest.StoreId, 0));
			Currency val4 = ((val3 == null || !val3.Published) ? (await _workContext.GetWorkingCurrencyAsync()) : val3);
			Currency customerCurrency = val4;
			Currency val5 = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
			string customerCurrencyCode = customerCurrency.CurrencyCode.ToLower();
			decimal num = customerCurrency.Rate / val5.Rate;
			int paymentAmount = ((!Array.Exists(Stripe3DSecurePaymentDefaults.ZeroDecimal, (string z) => z.ToLower() == customerCurrencyCode)) ? ((int)Math.Ceiling(await _priceCalculationService.RoundPriceAsync(_currencyService.ConvertCurrency(processPaymentRequest.OrderTotal, num), (Currency)null) * 100m)) : ((int)Math.Ceiling(await _priceCalculationService.RoundPriceAsync(_currencyService.ConvertCurrency(processPaymentRequest.OrderTotal, num), (Currency)null))));
			PlanCreateOptions planCreateOptions = new PlanCreateOptions();
			PlanCreateOptions planCreateOptions2 = planCreateOptions;
			planCreateOptions2.Nickname = string.Format(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Fields.StripePlan.Nickname"), processPaymentRequest.OrderGuid.ToString());
			planCreateOptions.Amount = paymentAmount;
			planCreateOptions.Currency = customerCurrencyCode;
			PlanCreateOptions planCreateOptions3 = planCreateOptions;
			planCreateOptions3.Interval = (await _localizationService.GetLocalizedEnumAsync<RecurringProductCyclePeriod>(product.RecurringCyclePeriod, (int?)null)).TrimEnd('s').ToLower();
			planCreateOptions.IntervalCount = processPaymentRequest.RecurringCycleLength;
			planCreateOptions.Product = stripeProductId;
			PlanCreateOptions options4 = planCreateOptions;
			PlanService planService = new PlanService();
			Plan stripePlan = planService.Create(options4);
			CustomerService customerService = new CustomerService();
			genericAttributeService = _genericAttributeService;
			string text = await genericAttributeService.GetAttributeAsync<string>((BaseEntity)(object)(await _workContext.GetCurrentCustomerAsync()), Stripe3DSecurePaymentDefaults.StripeCustomerKey, processPaymentRequest.StoreId, (string)null);
			Stripe.Customer stripeCustomer;
			if (string.IsNullOrEmpty(text))
			{
				IAddressService addressService = _addressService;
				var billingAddress = await addressService.GetAddressByIdAsync((await _workContext.GetCurrentCustomerAsync()).BillingAddressId.GetValueOrDefault());
				var val6 = await _workContext.GetCurrentCustomerAsync();
				object obj = ((val6 != null) ? val6.Email : null);
				if (obj == null)
				{
					obj = ((billingAddress != null) ? billingAddress.Email : null);
				}
				string email = (string)obj;
				CustomerCreateOptions customerCreateOptions = new CustomerCreateOptions
				{
					Email = email
				};
				CustomerCreateOptions customerCreateOptions2 = customerCreateOptions;
				string format = await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.CustomerOptions.Description");
				Store val7 = await _storeService.GetStoreByIdAsync(processPaymentRequest.StoreId);
				customerCreateOptions2.Description = string.Format(format, (val7 != null) ? val7.Name : null);
				stripeCustomer = customerService.Create(customerCreateOptions);
				genericAttributeService = _genericAttributeService;
				await genericAttributeService.SaveAttributeAsync<string>((BaseEntity)(object)(await _workContext.GetCurrentCustomerAsync()), Stripe3DSecurePaymentDefaults.StripeCustomerKey, stripeCustomer.Id, processPaymentRequest.StoreId);
			}
			else
			{
				stripeCustomer = customerService.Get(text);
			}
			if (stripeCustomer != null)
			{
				CustomerUpdateOptions options5 = new CustomerUpdateOptions
				{
					InvoiceSettings = new CustomerInvoiceSettingsOptions
					{
						DefaultPaymentMethod = paymentIntent.PaymentMethodId
					}
				};
				customerService.Update(paymentIntent.CustomerId, options5);
				DateTime value = DateTime.UtcNow;
				RecurringProductCyclePeriod recurringCyclePeriod = processPaymentRequest.RecurringCyclePeriod;
				if ((int)recurringCyclePeriod <= 10)
				{
					if ((int)recurringCyclePeriod != 0)
					{
						if ((int)recurringCyclePeriod == 10)
						{
							value = value.AddDays(processPaymentRequest.RecurringCycleLength * 7);
						}
					}
					else
					{
						value = value.AddDays(processPaymentRequest.RecurringCycleLength);
					}
				}
				else if ((int)recurringCyclePeriod != 20)
				{
					if ((int)recurringCyclePeriod == 30)
					{
						value = value.AddYears(processPaymentRequest.RecurringCycleLength);
					}
				}
				else
				{
					value = value.AddMonths(processPaymentRequest.RecurringCycleLength);
				}
				SubscriptionCreateOptions options6 = new SubscriptionCreateOptions
				{
					Customer = paymentIntent.CustomerId,
					Items = new List<SubscriptionItemOptions>
					{
						new SubscriptionItemOptions
						{
							Plan = stripePlan.Id
						}
					},
					Metadata = new Dictionary<string, string> { 
					{
						"OrderGuid",
						processPaymentRequest.OrderGuid.ToString()
					} },
					TrialEnd = value,
					OffSession = true
				};
				Subscription subscription = new SubscriptionService().Create(options6);
				if (subscription.Status == Stripe3DSecurePaymentDefaults.StripeSubscriptionStatusTraialing)
				{
					result.AuthorizationTransactionId = paymentIntent.Id;
					result.AuthorizationTransactionResult = paymentIntent.Status;
					result.SubscriptionTransactionId = subscription.Id;
					result.NewPaymentStatus = (PaymentStatus)20;
				}
			}
		}
		catch (StripeException ex)
		{
			if (!string.IsNullOrEmpty(ex.StripeError?.Code))
			{
				((BaseNopResult)result).AddError($"{ex.StripeError.Message} Error code: {ex.StripeError.Code}");
			}
			else
			{
				((BaseNopResult)result).AddError(ex.StripeError.Message);
			}
		}
		return result;
	}

	public async Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
	{
		CancelRecurringPaymentResult result = new CancelRecurringPaymentResult();
		try
		{
			await new SubscriptionService().CancelAsync(cancelPaymentRequest.Order.SubscriptionTransactionId, new SubscriptionCancelOptions());
		}
		catch (StripeException ex)
		{
			if (!string.IsNullOrEmpty(ex.StripeError?.Code))
			{
				((BaseNopResult)result).AddError($"{ex.StripeError.Message} Error code: {ex.StripeError.Code}");
			}
			else
			{
				((BaseNopResult)result).AddError(ex.StripeError.Message);
			}
		}
		return result;
	}
}
