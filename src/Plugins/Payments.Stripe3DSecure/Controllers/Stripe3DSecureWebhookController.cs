using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Tax;
using Nop.Core.Events;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.Stripe3DSecure.Domain;
using Nop.Plugin.Payments.Stripe3DSecure.Services;
using Nop.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;
using Stripe;
using ILogger = Nop.Services.Logging.ILogger;
using SessionExtensions = Nop.Core.Http.Extensions.SessionExtensions;

namespace Nop.Plugin.Payments.Stripe3DSecure.Controllers;

public class Stripe3DSecureWebhookController : BaseController
{
	protected class PlaceOrderContainer
	{
		public Core.Domain.Customers.Customer Customer { get; set; }

		public Language CustomerLanguage { get; set; }

		public int AffiliateId { get; set; }

        public TaxDisplayType CustomerTaxDisplayType { get; set; }

        public string CustomerCurrencyCode { get; set; }

		public decimal CustomerCurrencyRate { get; set; }

		public Core.Domain.Common.Address BillingAddress { get; set; }

		public Core.Domain.Common.Address ShippingAddress { get; set; }

		public ShippingStatus ShippingStatus { get; set; }

		public string ShippingMethodName { get; set; }

		public string ShippingRateComputationMethodSystemName { get; set; }

		public bool PickupInStore { get; set; }

		public Core.Domain.Common.Address PickupAddress { get; set; }

		public bool IsRecurringShoppingCart { get; set; }

		public Order InitialOrder { get; set; }

		public string CheckoutAttributeDescription { get; set; }

		public string CheckoutAttributesXml { get; set; }

		public IList<ShoppingCartItem> Cart { get; set; }

		public List<Discount> AppliedDiscounts { get; set; }

		public List<AppliedGiftCard> AppliedGiftCards { get; set; }

		public decimal OrderSubTotalInclTax { get; set; }

		public decimal OrderSubTotalExclTax { get; set; }

		public decimal OrderSubTotalDiscountInclTax { get; set; }

		public decimal OrderSubTotalDiscountExclTax { get; set; }

		public decimal OrderShippingTotalInclTax { get; set; }

		public decimal OrderShippingTotalExclTax { get; set; }

		public decimal PaymentAdditionalFeeInclTax { get; set; }

		public decimal PaymentAdditionalFeeExclTax { get; set; }

		public decimal OrderTaxTotal { get; set; }

		public string VatNumber { get; set; }

		public string TaxRates { get; set; }

		public decimal OrderDiscountAmount { get; set; }

		public int RedeemedRewardPoints { get; set; }

		public decimal RedeemedRewardPointsAmount { get; set; }

		public decimal OrderTotal { get; set; }

		public PlaceOrderContainer()
		{
			Cart = new List<ShoppingCartItem>();
			AppliedDiscounts = new List<Discount>();
			AppliedGiftCards = new List<AppliedGiftCard>();
		}
	}

	protected readonly ISettingService _settingService;

	protected readonly ILogger _logger;

	protected readonly IStoreContext _storeContext;

	protected readonly IOrderService _orderService;

	protected readonly IStripe3DSecureService _stripe3DSecureService;

	protected readonly IOrderProcessingService _orderProcessingService;

	protected readonly IWorkflowMessageService _workflowMessageService;

	protected readonly LocalizationSettings _localizationSettings;

	protected readonly IEventPublisher _eventPublisher;

	protected readonly OrderSettings _orderSettings;

	protected readonly IShoppingCartService _shoppingCartService;

	protected readonly IWorkContext _workContext;

	protected readonly ICurrencyService _currencyService;

	protected readonly IPriceFormatter _priceFormatter;

	protected readonly IGenericAttributeService _genericAttributeService;

	protected readonly ShippingSettings _shippingSettings;

	protected readonly IOrderTotalCalculationService _orderTotalCalculationService;

	protected readonly ILocalizationService _localizationService;

	protected readonly Stripe3DSecureManager _stripe3DSecureManager;

	protected readonly IPriceCalculationService _priceCalculationService;

	protected readonly ICustomerService _customerService;

	protected readonly IAddressService _addressService;

	protected readonly ICountryService _countryService;

	protected readonly IProductService _productService;

	public Stripe3DSecureWebhookController(ISettingService settingService, ILogger logger, IStoreContext storeContext, IOrderService orderService, IStripe3DSecureService stripe3DSecureService, IOrderProcessingService orderProcessingService, IWorkflowMessageService workflowMessageService, LocalizationSettings localizationSettings, IEventPublisher eventPublisher, OrderSettings orderSettings, IShoppingCartService shoppingCartService, IWorkContext workContext, ICurrencyService currencyService, IPriceFormatter priceFormatter, IGenericAttributeService genericAttributeService, ShippingSettings shippingSettings, IOrderTotalCalculationService orderTotalCalculationService, ILocalizationService localizationService, Stripe3DSecureManager stripe3DSecureManager, IPriceCalculationService priceCalculationService, ICustomerService customerService, IAddressService addressService, ICountryService countryService, IProductService productService)
	{
		_settingService = settingService;
		_logger = logger;
		_storeContext = storeContext;
		_orderService = orderService;
		_stripe3DSecureService = stripe3DSecureService;
		_orderProcessingService = orderProcessingService;
		_workflowMessageService = workflowMessageService;
		_localizationSettings = localizationSettings;
		_eventPublisher = eventPublisher;
		_orderSettings = orderSettings;
		_shoppingCartService = shoppingCartService;
		_workContext = workContext;
		_currencyService = currencyService;
		_priceFormatter = priceFormatter;
		_genericAttributeService = genericAttributeService;
		_shippingSettings = shippingSettings;
		_orderTotalCalculationService = orderTotalCalculationService;
		_localizationService = localizationService;
		_stripe3DSecureManager = stripe3DSecureManager;
		_priceCalculationService = priceCalculationService;
		_customerService = customerService;
		_addressService = addressService;
		_countryService = countryService;
		_productService = productService;
	}

	protected virtual async Task<bool> IsMinimumOrderPlacementIntervalValidAsync(Core.Domain.Customers.Customer customer)
	{
		if (_orderSettings.MinimumOrderPlacementInterval == 0)
		{
			return true;
		}
		Store val = await _storeContext.GetCurrentStoreAsync();
		Order val2 = ((IEnumerable<Order>)(await _orderService.SearchOrdersAsync(((BaseEntity)val).Id, 0, ((BaseEntity)customer).Id, 0, 0, 0, 0, (string)null, (DateTime?)null, (DateTime?)null, (List<int>)null, (List<int>)null, (List<int>)null, (string)null, (string)null, "", (string)null, 0, 1, false))).FirstOrDefault();
		if (val2 == null)
		{
			return true;
		}
		return (DateTime.UtcNow - val2.CreatedOnUtc).TotalSeconds > (double)_orderSettings.MinimumOrderPlacementInterval;
	}

	protected virtual async Task<Stripe3DSecurePaymentResponce> CreateStripePaymentIntentAsync(decimal? shoppingCartTotal, bool isRecurringShoppingCart)
	{
		var customer = await _workContext.GetCurrentCustomerAsync();
		Currency workingCurrency = await _workContext.GetWorkingCurrencyAsync();
		IPriceCalculationService priceCalculationService = _priceCalculationService;
		decimal priceTotal = await priceCalculationService.RoundPriceAsync(await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotal.Value, workingCurrency), (Currency)null);
		string paymentIntentId = await SessionExtensions.GetAsync<string>(((ControllerBase)this).HttpContext.Session, Stripe3DSecurePaymentDefaults.PaymentIntentKey);
		Stripe3DSecurePaymentIntentRequest paymentIntentRequest = new Stripe3DSecurePaymentIntentRequest
		{
			PaymentIntentId = paymentIntentId,
			OrderGuid = Guid.NewGuid(),
			OrderTotal = preparePrice(priceTotal),
			CurrencyCode = workingCurrency.CurrencyCode.ToLower(),
			Email = customer.Email,
			CustomerId = ((BaseEntity)customer).Id,
			CaptureMethod = "manual",
			IsRecurring = isRecurringShoppingCart
		};
		Stripe3DSecurePaymentResponce paymentIntentSecret = await _stripe3DSecureManager.CreatePaymentIntentSecretAsync(paymentIntentRequest);
		if (!paymentIntentSecret.Warnings.Any())
		{
			await SessionExtensions.SetAsync<string>(((ControllerBase)this).HttpContext.Session, Stripe3DSecurePaymentDefaults.PaymentIntentKey, paymentIntentSecret.PaymentIntentId);
		}
		return paymentIntentSecret;
		int preparePrice(decimal price)
		{
			if (Array.Exists(Stripe3DSecurePaymentDefaults.ZeroDecimal, (string z) => z == workingCurrency.CurrencyCode.ToUpper()))
			{
				return Convert.ToInt32(Math.Ceiling(price));
			}
			return Convert.ToInt32(Math.Ceiling(price * 100m));
		}
	}

	[HttpPost]
	public async Task<IActionResult> WebhookEventsHandler()
	{
		Store val = await _storeContext.GetCurrentStoreAsync();
		Stripe3DSecurePaymentSettings stripe3DSecurePaymentSettings = await _settingService.LoadSettingAsync<Stripe3DSecurePaymentSettings>((val != null) ? ((BaseEntity)val).Id : 0);
		if (stripe3DSecurePaymentSettings == null || string.IsNullOrEmpty(stripe3DSecurePaymentSettings.WebhookId))
		{
			stripe3DSecurePaymentSettings = await _settingService.LoadSettingAsync<Stripe3DSecurePaymentSettings>(0);
		}
		StripeConfiguration.ApiKey = stripe3DSecurePaymentSettings.SecretApiKey;
		StripeConfiguration.AppInfo = new AppInfo
		{
			Name = "Card Payment(3DSecure) - nopCommercePlus(Forefront Infotech)",
			Version = "4.00",
			Url = "http://www.nopcommerceplus.com/stripe-3d-secure"
		};
		try
		{
			string json = new StreamReader(((ControllerBase)this).HttpContext.Request.Body).ReadToEnd();
			Event stripeEvent = EventUtility.ConstructEvent(json, ((ControllerBase)this).Request.Headers["Stripe-Signature"], stripe3DSecurePaymentSettings.SigningSecretKey, 300L);
			if (stripeEvent.Type == Stripe3DSecurePaymentDefaults.PaymentIntentSuccess)
			{
				PaymentIntent paymentIntent = stripeEvent.Data.Object as PaymentIntent;
				ChargeService chargeService = new ChargeService();
				Charge stripeCharge = chargeService.Get(paymentIntent.LatestChargeId);
				if (stripeCharge != null)
				{
					InvoicePayment invoicePayment = new InvoicePaymentService().List(new InvoicePaymentListOptions
					{
						Payment = new InvoicePaymentPaymentOptions
						{
							Type = "payment_intent",
							PaymentIntent = paymentIntent.Id
						},
						Limit = 1L
					}).Data.FirstOrDefault();
					Guid result;
					if (invoicePayment != null && !string.IsNullOrEmpty(invoicePayment.InvoiceId))
					{
						Invoice invoice = new InvoiceService().Get(invoicePayment.InvoiceId);
						if (invoice != null)
						{
							Order initialOrder = await _stripe3DSecureService.GetOrderBySubscriptionTransactionIdAndPaymentMethodAsync(invoice.Parent?.SubscriptionDetails?.SubscriptionId);
							if (initialOrder != null)
							{
								RecurringPayment recurringPayment = ((IEnumerable<RecurringPayment>)(await _orderService.SearchRecurringPaymentsAsync(0, 0, ((BaseEntity)initialOrder).Id, null, 0, int.MaxValue, false))).FirstOrDefault();
								if (recurringPayment != null)
								{
									if (!stripeCharge.Paid)
									{
										string text = $"Card Payment(3DSecure) : failure code {stripeCharge.FailureCode}, failure_message {stripeCharge.FailureMessage} for the order #{((BaseEntity)initialOrder).Id}";
										await _logger.ErrorAsync(text, (Exception)null, null);
										return ((ControllerBase)(object)this).BadRequest(text);
									}
									IList<RecurringPaymentHistory> source = await _orderService.GetRecurringPaymentHistoryAsync(recurringPayment);
									if (!source.Any())
									{
										initialOrder.CaptureTransactionId = stripeCharge.Id;
										initialOrder.CaptureTransactionResult = stripeCharge.Status;
										await _orderService.UpdateOrderAsync(initialOrder);
										await _orderService.InsertRecurringPaymentHistoryAsync(new RecurringPaymentHistory
										{
											RecurringPaymentId = ((BaseEntity)recurringPayment).Id,
											OrderId = ((BaseEntity)initialOrder).Id,
											CreatedOnUtc = DateTime.UtcNow
										});
									}
									else if (!(await _orderService.GetOrdersByIdsAsync(source.Select((RecurringPaymentHistory order) => order.OrderId).ToArray())).Any((Order order) => !string.IsNullOrEmpty(order.CaptureTransactionId) && order.CaptureTransactionId.Equals(stripeCharge.Id, StringComparison.InvariantCultureIgnoreCase)))
									{
										ProcessPaymentResult val2 = new ProcessPaymentResult
										{
											NewPaymentStatus = (PaymentStatus)30,
											CaptureTransactionId = stripeCharge.Id,
											CaptureTransactionResult = stripeCharge.Status
										};
										if (!(await _orderProcessingService.ProcessNextRecurringPaymentAsync(recurringPayment, val2)).Any())
										{
											Order newOrder = await _stripe3DSecureService.GetOrderByCaptureTransactionIdAsync(stripeCharge.Id);
											if (newOrder != null)
											{
												PaymentIntentService paymentIntentService = new PaymentIntentService();
												PaymentIntentUpdateOptions paymentIntentUpdateOptions = new PaymentIntentUpdateOptions();
												PaymentIntentUpdateOptions paymentIntentUpdateOptions2 = paymentIntentUpdateOptions;
												paymentIntentUpdateOptions2.Description = string.Format(await _localizationService.GetResourceAsync("Plugin.Payments.Stripe3DSecure.Update.Description"), (await _storeContext.GetCurrentStoreAsync()).Name, newOrder.CustomOrderNumber);
												paymentIntentService.Update(paymentIntent.Id, paymentIntentUpdateOptions);
											}
										}
									}
								}
							}
						}
					}
					else if (Guid.TryParse(stripeCharge.Metadata["OrderGuid"], out result))
					{
						Order initialOrder = await _orderService.GetOrderByGuidAsync(result);
						if (initialOrder != null && (int)initialOrder.PaymentStatus == 20 && initialOrder.PaymentMethodSystemName == Stripe3DSecurePaymentDefaults.PluginMethodSystemName)
						{
							initialOrder.CaptureTransactionId = stripeCharge.Id;
							initialOrder.CaptureTransactionResult = stripeCharge.Status;
							await _orderService.UpdateOrderAsync(initialOrder);
							if ((DateTime.UtcNow - initialOrder.CreatedOnUtc).TotalSeconds > 10.0 && _orderProcessingService.CanMarkOrderAsPaid(initialOrder))
							{
								await _orderProcessingService.MarkOrderAsPaidAsync(initialOrder);
							}
						}
					}
				}
			}
			if (stripeEvent.Type == Stripe3DSecurePaymentDefaults.PaymentIntentPaymentFailed)
			{
				PaymentIntent paymentIntent2 = stripeEvent.Data.Object as PaymentIntent;
				if (string.IsNullOrEmpty(paymentIntent2.LatestChargeId))
				{
					return ((ControllerBase)(object)this).Ok();
				}
				ChargeService chargeService2 = new ChargeService();
				Charge stripeCharge2 = chargeService2.Get(paymentIntent2.LatestChargeId);
				if (stripeCharge2 != null)
				{
					InvoicePayment invoicePayment2 = new InvoicePaymentService().List(new InvoicePaymentListOptions
					{
						Payment = new InvoicePaymentPaymentOptions
						{
							Type = "payment_intent",
							PaymentIntent = paymentIntent2.Id
						},
						Limit = 1L
					}).Data.FirstOrDefault();
					if (invoicePayment2 != null && !string.IsNullOrEmpty(invoicePayment2.InvoiceId))
					{
						Invoice invoice2 = new InvoiceService().Get(invoicePayment2.InvoiceId);
						if (invoice2 != null)
						{
							Order initialOrder = await _stripe3DSecureService.GetOrderBySubscriptionTransactionIdAndPaymentMethodAsync(invoice2.Parent?.SubscriptionDetails?.SubscriptionId);
							if (initialOrder != null)
							{
								RecurringPayment val3 = ((IEnumerable<RecurringPayment>)(await _orderService.SearchRecurringPaymentsAsync(0, 0, ((BaseEntity)initialOrder).Id, null, 0, int.MaxValue, false))).FirstOrDefault();
								if (val3 != null)
								{
									if (!stripeCharge2.Paid)
									{
										string text = $"Card Payment(3DSecure) : failure code {stripeCharge2.FailureCode}, failure_message {stripeCharge2.FailureMessage} for the order #{((BaseEntity)initialOrder).Id}";
										await _logger.ErrorAsync(text, (Exception)null, null);
										return ((ControllerBase)(object)this).BadRequest(text);
									}
									IOrderProcessingService orderProcessingService = _orderProcessingService;
									ProcessPaymentResult val4 = new ProcessPaymentResult();
									((BaseNopResult)val4).Errors = new string[2] { stripeCharge2.FailureCode, stripeCharge2.FailureMessage };
									val4.RecurringPaymentFailed = true;
									await orderProcessingService.ProcessNextRecurringPaymentAsync(val3, val4);
								}
							}
						}
					}
				}
			}
			if (stripeEvent.Type == Stripe3DSecurePaymentDefaults.ChargeRefunded)
			{
				Charge stripeCharge3 = stripeEvent.Data.Object as Charge;
				if (stripeCharge3 != null && !stripeCharge3.Metadata.Any())
				{
					Order initialOrder = await _stripe3DSecureService.GetOrderByCaptureTransactionIdAsync(stripeCharge3.Id);
					if (initialOrder != null)
					{
						decimal num = ((!Array.Exists(Stripe3DSecurePaymentDefaults.ZeroDecimal, (string z) => z.ToLower() == stripeCharge3.Currency.ToLower())) ? Convert.ToDecimal((decimal)stripeCharge3.AmountRefunded / 100m) : Convert.ToDecimal(stripeCharge3.AmountRefunded));
						if (initialOrder.RefundedAmount != num)
						{
							Refund refund = stripeCharge3.Refunds.FirstOrDefault();
							decimal amountToRefund = ((!Array.Exists(Stripe3DSecurePaymentDefaults.ZeroDecimal, (string z) => z.ToLower() == stripeCharge3.Currency.ToLower())) ? Convert.ToDecimal((decimal)refund.Amount / 100m) : Convert.ToDecimal(refund.Amount));
							decimal num2 = (initialOrder.RefundedAmount += amountToRefund);
							initialOrder.PaymentStatus = (PaymentStatus)((initialOrder.OrderTotal == num2) ? 40 : 35);
							await _orderService.UpdateOrderAsync(initialOrder);
							if ((int)initialOrder.PaymentStatus != 35)
							{
								await _orderService.InsertOrderNoteAsync(new OrderNote
								{
									OrderId = ((BaseEntity)initialOrder).Id,
									Note = $"Order has been marked as refunded. Amount = {amountToRefund}",
									DisplayToCustomer = false,
									CreatedOnUtc = DateTime.UtcNow
								});
							}
							else
							{
								await _orderService.InsertOrderNoteAsync(new OrderNote
								{
									OrderId = ((BaseEntity)initialOrder).Id,
									Note = $"Order has been partially refunded. Amount = {amountToRefund}",
									DisplayToCustomer = false,
									CreatedOnUtc = DateTime.UtcNow
								});
							}
							await _orderProcessingService.CheckOrderStatusAsync(initialOrder);
							IList<int> list = await _workflowMessageService.SendOrderRefundedStoreOwnerNotificationAsync(initialOrder, amountToRefund, _localizationSettings.DefaultAdminLanguageId);
							if (list.Any())
							{
								await _orderService.InsertOrderNoteAsync(new OrderNote
								{
									OrderId = ((BaseEntity)initialOrder).Id,
									Note = "\"Order refunded\" email (to store owner) has been queued. Queued email identifiers: " + string.Join(", ", list) + ".",
									DisplayToCustomer = false,
									CreatedOnUtc = DateTime.UtcNow
								});
							}
							IList<int> list2 = await _workflowMessageService.SendOrderRefundedCustomerNotificationAsync(initialOrder, amountToRefund, initialOrder.CustomerLanguageId);
							if (list2.Any())
							{
								await _orderService.InsertOrderNoteAsync(new OrderNote
								{
									OrderId = ((BaseEntity)initialOrder).Id,
									Note = "\"Order refunded\" email (to customer) has been queued. Queued email identifiers: " + string.Join(", ", list2) + ".",
									DisplayToCustomer = false,
									CreatedOnUtc = DateTime.UtcNow
								});
							}
							await _eventPublisher.PublishAsync<OrderRefundedEvent>(new OrderRefundedEvent(initialOrder, amountToRefund));
						}
					}
				}
			}
			if (stripeEvent.Type == Stripe3DSecurePaymentDefaults.CustomerSubscriptionCreated && stripeEvent.Data.Object is Subscription stripeSubscription)
			{
				Order initialOrder = await _stripe3DSecureService.GetOrderBySubscriptionTransactionIdAndPaymentMethodAsync(stripeSubscription.Id);
				if (initialOrder != null)
				{
					RecurringPayment recurringPayment = ((IEnumerable<RecurringPayment>)(await _orderService.SearchRecurringPaymentsAsync(0, 0, ((BaseEntity)initialOrder).Id, null, 0, int.MaxValue, false))).FirstOrDefault();
					if (recurringPayment != null && stripeSubscription.Status == Stripe3DSecurePaymentDefaults.StripeSubscriptionStatusTraialing && !(await _orderService.GetRecurringPaymentHistoryAsync(recurringPayment)).Any() && (DateTime.UtcNow - initialOrder.CreatedOnUtc).TotalSeconds > 10.0)
					{
						await _orderService.InsertRecurringPaymentHistoryAsync(new RecurringPaymentHistory
						{
							RecurringPaymentId = ((BaseEntity)recurringPayment).Id,
							OrderId = ((BaseEntity)initialOrder).Id,
							CreatedOnUtc = DateTime.UtcNow
						});
					}
				}
			}
			if (stripeEvent.Type == Stripe3DSecurePaymentDefaults.CustomerSubscriptionDeleted && stripeEvent.Data.Object is Subscription subscription)
			{
				Order initialOrder = await _stripe3DSecureService.GetOrderBySubscriptionTransactionIdAndPaymentMethodAsync(subscription.Id);
				if (initialOrder != null)
				{
					RecurringPayment recurringPayment = ((IEnumerable<RecurringPayment>)(await _orderService.SearchRecurringPaymentsAsync(0, 0, ((BaseEntity)initialOrder).Id, null, 0, int.MaxValue, false))).FirstOrDefault();
					if (recurringPayment.IsActive)
					{
						recurringPayment.IsActive = false;
						await _orderService.UpdateRecurringPaymentAsync(recurringPayment);
						await _orderService.InsertOrderNoteAsync(new OrderNote
						{
							OrderId = ((BaseEntity)initialOrder).Id,
							Note = "Recurring payment has been cancelled",
							DisplayToCustomer = false,
							CreatedOnUtc = DateTime.UtcNow
						});
						await _workflowMessageService.SendRecurringPaymentCancelledStoreOwnerNotificationAsync(recurringPayment, _localizationSettings.DefaultAdminLanguageId);
					}
				}
			}
			return ((ControllerBase)(object)this).Ok();
		}
		catch (Exception ex)
		{
			await _logger.ErrorAsync($"Card Payment(3DSecure) : {((ex.InnerException != null) ? ex.InnerException.Message : ex.Message)}", ex, null);
			return ((ControllerBase)(object)this).BadRequest(ex);
		}
	}

	public virtual async Task<IActionResult> ValidateConfirmOrder()
	{
		try
		{
			if (_orderSettings.CheckoutDisabled)
			{
				throw new Exception(await _localizationService.GetResourceAsync("Checkout.Disabled"));
			}
			var customer = await _workContext.GetCurrentCustomerAsync();
			var shoppingCartService = _shoppingCartService;
			var val = customer;
			if (!(await shoppingCartService.GetShoppingCartAsync(val, (ShoppingCartType)1, ((BaseEntity)(await _storeContext.GetCurrentStoreAsync())).Id, (int?)null, (DateTime?)null, (DateTime?)null, (int?)null)).Any())
			{
				throw new Exception("Your cart is empty");
			}
			if (await _customerService.IsGuestAsync(customer, true) && !_orderSettings.AnonymousCheckoutAllowed)
			{
				throw new Exception("Anonymous checkout is not allowed");
			}
			if (!(await IsMinimumOrderPlacementIntervalValidAsync(customer)))
			{
				throw new Exception(await _localizationService.GetResourceAsync("Checkout.MinOrderPlacementInterval"));
			}
			PlaceOrderContainer details = new PlaceOrderContainer
			{
				Customer = customer
			};
			if (details.Customer == null)
			{
				throw new ArgumentException("Customer is not set");
			}
			if (await _customerService.IsGuestAsync(details.Customer, true) && !_orderSettings.AnonymousCheckoutAllowed)
			{
				throw new NopException("Anonymous checkout is not allowed");
			}
			var val2 = await _customerService.GetCustomerBillingAddressAsync(details.Customer);
			if (!CommonHelper.IsValidEmail(val2?.Email))
			{
				throw new NopException("Email is not valid");
			}
			details.BillingAddress = _addressService.CloneAddress(val2);
			Country val3 = await _countryService.GetCountryByAddressAsync(details.BillingAddress);
			if (val3 != null && !val3.AllowsBilling)
			{
				throw new NopException("Country '" + val3.Name + "' is not allowed for billing");
			}
			PlaceOrderContainer placeOrderContainer = details;
			shoppingCartService = _shoppingCartService;
			val = details.Customer;
			placeOrderContainer.Cart = await shoppingCartService.GetShoppingCartAsync(val, (ShoppingCartType)1, ((BaseEntity)(await _storeContext.GetCurrentStoreAsync())).Id, (int?)null, (DateTime?)null, (DateTime?)null, (int?)null);
			if (!details.Cart.Any())
			{
				throw new NopException("Cart is empty");
			}
			placeOrderContainer = details;
			IGenericAttributeService genericAttributeService = _genericAttributeService;
			BaseEntity customer2 = (BaseEntity)(object)details.Customer;
			string checkoutAttributes = NopCustomerDefaults.CheckoutAttributes;
			placeOrderContainer.CheckoutAttributesXml = await genericAttributeService.GetAttributeAsync<string>(customer2, checkoutAttributes, ((BaseEntity)(await _storeContext.GetCurrentStoreAsync())).Id, (string)null);
			IList<string> source = await _shoppingCartService.GetShoppingCartWarningsAsync(details.Cart, details.CheckoutAttributesXml, true);
			if (source.Any())
			{
				throw new NopException(source.Aggregate(string.Empty, (string current, string next) => current + next + ";"));
			}
			foreach (ShoppingCartItem sci in details.Cart)
			{
				var val4 = await _productService.GetProductByIdAsync(sci.ProductId);
				shoppingCartService = _shoppingCartService;
				val = details.Customer;
				ShoppingCartType shoppingCartType = sci.ShoppingCartType;
				var val5 = val4;
				IList<string> source2 = await shoppingCartService.GetShoppingCartItemWarningsAsync(val, shoppingCartType, val5, ((BaseEntity)(await _storeContext.GetCurrentStoreAsync())).Id, sci.AttributesXml, sci.CustomerEnteredPrice, sci.RentalStartDateUtc, sci.RentalEndDateUtc, sci.Quantity, false, ((BaseEntity)sci).Id, true, true, true, true, true);
				if (source2.Any())
				{
					throw new NopException(source2.Aggregate(string.Empty, (string current, string next) => current + next + ";"));
				}
			}
			if (!(await _orderProcessingService.ValidateMinOrderSubtotalAmountAsync(details.Cart)))
			{
				ICurrencyService currencyService = _currencyService;
				decimal minOrderSubtotalAmount = _orderSettings.MinOrderSubtotalAmount;
				decimal minOrderSubtotalAmount2 = await currencyService.ConvertFromPrimaryStoreCurrencyAsync(minOrderSubtotalAmount, await _workContext.GetWorkingCurrencyAsync());
				throw new NopException(string.Format(await _localizationService.GetResourceAsync("Checkout.MinOrderSubtotalAmount"), await _priceFormatter.FormatPriceAsync(minOrderSubtotalAmount2, true, false)));
			}
			if (!(await _orderProcessingService.ValidateMinOrderTotalAmountAsync(details.Cart)))
			{
				ICurrencyService currencyService = _currencyService;
				decimal minOrderSubtotalAmount = _orderSettings.MinOrderTotalAmount;
				decimal minOrderSubtotalAmount2 = await currencyService.ConvertFromPrimaryStoreCurrencyAsync(minOrderSubtotalAmount, await _workContext.GetWorkingCurrencyAsync());
				throw new NopException(string.Format(await _localizationService.GetResourceAsync("Checkout.MinOrderTotalAmount"), await _priceFormatter.FormatPriceAsync(minOrderSubtotalAmount2, true, false)));
			}
			if (await _shoppingCartService.ShoppingCartRequiresShippingAsync(details.Cart))
			{
				genericAttributeService = _genericAttributeService;
				customer2 = (BaseEntity)(object)details.Customer;
				checkoutAttributes = NopCustomerDefaults.SelectedPickupPointAttribute;
				PickupPoint val6 = await genericAttributeService.GetAttributeAsync<PickupPoint>(customer2, checkoutAttributes, ((BaseEntity)(await _storeContext.GetCurrentStoreAsync())).Id, (PickupPoint)null);
				if (!_shippingSettings.AllowPickupInStore && val6 == null)
				{
					if (!details.Customer.ShippingAddressId.HasValue)
					{
						throw new NopException("Shipping address is not provided");
					}
					var val7 = await _customerService.GetCustomerShippingAddressAsync(details.Customer);
					if (!CommonHelper.IsValidEmail(val7?.Email))
					{
						throw new NopException("Email is not valid");
					}
					details.ShippingAddress = _addressService.CloneAddress(val7);
					Country val8 = await _countryService.GetCountryByAddressAsync(details.ShippingAddress);
					if (val8 != null && !val8.AllowsShipping)
					{
						throw new NopException("Country '" + val8.Name + "' is not allowed for shipping");
					}
				}
			}
			decimal? orderShippingTotalInclTax = (await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(details.Cart, true)).Item1;
			decimal? item = (await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(details.Cart, false)).Item1;
			if (!orderShippingTotalInclTax.HasValue || !item.HasValue)
			{
				throw new NopException("Shipping total couldn't be calculated");
			}
			decimal? orderTotal = (await _orderTotalCalculationService.GetShoppingCartTotalAsync(details.Cart, (bool?)null, true)).Item1;
			if (!orderTotal.HasValue)
			{
				throw new NopException("Order total couldn't be calculated");
			}
			new Stripe3DSecurePaymentResponce();
			placeOrderContainer = details;
			placeOrderContainer.IsRecurringShoppingCart = await _shoppingCartService.ShoppingCartIsRecurringAsync(details.Cart);
			Stripe3DSecurePaymentResponce stripe3DSecurePaymentResponce;
			if (!details.IsRecurringShoppingCart)
			{
				stripe3DSecurePaymentResponce = await CreateStripePaymentIntentAsync(orderTotal, isRecurringShoppingCart: false);
				if (!stripe3DSecurePaymentResponce.Warnings.Any())
				{
					return ((Controller)(object)this).Json((object?)new
					{
						error = 0,
						message = string.Empty,
						clientSecret = stripe3DSecurePaymentResponce.ClientSecret
					});
				}
				return ((Controller)(object)this).Json((object?)new
				{
					error = 1,
					message = string.Join(" \n ", stripe3DSecurePaymentResponce.Warnings),
					clientSecret = string.Empty
				});
			}
			string item2 = (await _shoppingCartService.GetRecurringCycleInfoAsync(details.Cart)).Item1;
			if (!string.IsNullOrEmpty(item2))
			{
				throw new NopException(item2);
			}
			stripe3DSecurePaymentResponce = await CreateStripePaymentIntentAsync(orderTotal, isRecurringShoppingCart: true);
			if (!stripe3DSecurePaymentResponce.Warnings.Any())
			{
				return ((Controller)(object)this).Json((object?)new
				{
					error = 0,
					message = string.Empty,
					clientSecret = stripe3DSecurePaymentResponce.ClientSecret
				});
			}
			return ((Controller)(object)this).Json((object?)new
			{
				error = 1,
				message = string.Join(" \n ", stripe3DSecurePaymentResponce.Warnings),
				clientSecret = string.Empty
			});
		}
		catch (Exception ex)
		{
			ILogger logger = _logger;
			string checkoutAttributes = ex.Message;
			Exception ex2 = ex;
			await logger.WarningAsync(checkoutAttributes, ex2, await _workContext.GetCurrentCustomerAsync());
			return ((Controller)(object)this).Json((object?)new
			{
				error = 1,
				message = ex.Message,
				clientSecret = string.Empty
			});
		}
	}
}
