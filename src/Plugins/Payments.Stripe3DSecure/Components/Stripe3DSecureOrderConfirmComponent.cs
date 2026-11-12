using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.Stripe3DSecure.Models;
using Nop.Services.Common;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Web.Framework.Components;
using SessionExtensions = Nop.Core.Http.Extensions.SessionExtensions;

namespace Nop.Plugin.Payments.Stripe3DSecure.Components;

public class Stripe3DSecureOrderConfirmComponent : NopViewComponent
{
	protected readonly IWorkContext _workContext;

	protected readonly IGenericAttributeService _genericAttributeService;

	protected readonly Stripe3DSecurePaymentSettings _stripe3DSecurePaymentSettings;

	protected readonly IShoppingCartService _shoppingCartService;

	protected readonly IStoreContext _storeContext;

	protected readonly IPaymentPluginManager _paymentPluginManager;

	protected readonly IHttpContextAccessor _httpContextAccessor;

	protected readonly IAddressService _addressService;

	protected readonly IOrderProcessingService _orderProcessingService;

	public Stripe3DSecureOrderConfirmComponent(IWorkContext workContext, IGenericAttributeService genericAttributeService, Stripe3DSecurePaymentSettings stripe3DSecurePaymentSettings, IShoppingCartService shoppingCartService, IStoreContext storeContext, IPaymentPluginManager paymentPluginManager, IHttpContextAccessor httpContextAccessor, IAddressService addressService, IOrderProcessingService orderProcessingService)
	{
		_workContext = workContext;
		_genericAttributeService = genericAttributeService;
		_stripe3DSecurePaymentSettings = stripe3DSecurePaymentSettings;
		_shoppingCartService = shoppingCartService;
		_storeContext = storeContext;
		_paymentPluginManager = paymentPluginManager;
		_httpContextAccessor = httpContextAccessor;
		_addressService = addressService;
		_orderProcessingService = orderProcessingService;
	}

	public async Task<IViewComponentResult> InvokeAsync()
	{
		OrderConfirmModel model = new OrderConfirmModel();
		if (string.IsNullOrEmpty(_stripe3DSecurePaymentSettings.PublishableApiKey))
		{
			return ((ViewComponent)this).Content(string.Empty);
		}
		Customer customer = await _workContext.GetCurrentCustomerAsync();
		Store store = await _storeContext.GetCurrentStoreAsync();
		string paymentMethodSystemName = await _genericAttributeService.GetAttributeAsync<string>((BaseEntity)(object)customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, ((BaseEntity)store).Id, (string)null);
		if (paymentMethodSystemName != Stripe3DSecurePaymentDefaults.PluginMethodSystemName)
		{
			return ((ViewComponent)this).Content(string.Empty);
		}
		ProcessPaymentRequest val = await _orderProcessingService.GetProcessPaymentRequestAsync();
		if ((val.PaymentMethodSystemName != null || ((IEnumerable<CustomValue>)val.CustomValues).Any()) && val.PaymentMethodSystemName != Stripe3DSecurePaymentDefaults.PluginMethodSystemName)
		{
			return ((ViewComponent)this).Content(string.Empty);
		}
		IList<ShoppingCartItem> cart = await _shoppingCartService.GetShoppingCartAsync(customer, (ShoppingCartType)1, ((BaseEntity)store).Id, (int?)null, (DateTime?)null, (DateTime?)null, (int?)null);
		if (!(await _paymentPluginManager.IsPluginActiveAsync(paymentMethodSystemName, customer, ((BaseEntity)store).Id)))
		{
			return ((ViewComponent)this).Content(string.Empty);
		}
		if (await (await ((IPluginManager<IPaymentMethod>)(object)_paymentPluginManager).LoadPluginBySystemNameAsync(Stripe3DSecurePaymentDefaults.PluginMethodSystemName, (Customer)null, 0)).HidePaymentMethodAsync(cart))
		{
			return ((ViewComponent)this).Content(string.Empty);
		}
		string paymentMethod = await SessionExtensions.GetAsync<string>(_httpContextAccessor.HttpContext.Session, Stripe3DSecurePaymentDefaults.PaymentMethodKey);
		if (string.IsNullOrEmpty(paymentMethod))
		{
			return ((ViewComponent)this).Content(string.Empty);
		}
		Address billingAddress = await _addressService.GetAddressByIdAsync(customer.BillingAddressId.GetValueOrDefault());
		Address val2 = await _addressService.GetAddressByIdAsync(customer.ShippingAddressId.GetValueOrDefault());
		model.PublishableApiKey = _stripe3DSecurePaymentSettings.PublishableApiKey;
		model.PaymentMethodId = paymentMethod;
		model.ReceiptEmail = ((billingAddress != null) ? billingAddress.Email : null) ?? ((val2 != null) ? val2.Email : null) ?? customer.Email ?? string.Empty;
		model.ShippingAddress = val2;
		OrderConfirmModel orderConfirmModel = model;
		orderConfirmModel.IsRecurring = await _shoppingCartService.ShoppingCartIsRecurringAsync(cart);
		orderConfirmModel = model;
		orderConfirmModel.IsRequiresShipping = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
		model.SentReceiptEmail = _stripe3DSecurePaymentSettings.SentReceiptEmail;
		model.PassShippingAddress = _stripe3DSecurePaymentSettings.PassShippingAddress;
		return ((ViewComponent)this).View("~/Plugins/Payments.Stripe3DSecure/Views/OrderConfirm.cshtml", model);
	}
}
