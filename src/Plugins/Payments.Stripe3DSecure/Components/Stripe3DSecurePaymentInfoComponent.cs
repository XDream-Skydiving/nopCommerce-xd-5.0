using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Stripe3DSecure.Models;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Stripe3DSecure.Components;

public class Stripe3DSecurePaymentInfoComponent : NopViewComponent
{
	protected readonly IWorkContext _workContext;

	protected readonly ICustomerService _customerService;

	protected readonly OrderSettings _orderSettings;

	protected readonly Stripe3DSecurePaymentSettings _stripe3DSecurePaymentSettings;

	protected readonly IShoppingCartService _shoppingCartService;

	protected readonly IStoreContext _storeContext;

	protected readonly IAddressService _addressService;

	public Stripe3DSecurePaymentInfoComponent(IWorkContext workContext, ICustomerService customerService, OrderSettings orderSettings, Stripe3DSecurePaymentSettings stripe3DSecurePaymentSettings, IShoppingCartService shoppingCartService, IStoreContext storeContext, IAddressService addressService)
	{
		_workContext = workContext;
		_customerService = customerService;
		_orderSettings = orderSettings;
		_stripe3DSecurePaymentSettings = stripe3DSecurePaymentSettings;
		_shoppingCartService = shoppingCartService;
		_storeContext = storeContext;
		_addressService = addressService;
	}

	public async Task<IViewComponentResult> InvokeAsync()
	{
		if (string.IsNullOrEmpty(_stripe3DSecurePaymentSettings.PublishableApiKey))
		{
			return ((ViewComponent)this).Content(string.Empty);
		}
		Customer customer = await _workContext.GetCurrentCustomerAsync();
		PaymentInfoModel paymentInfoModel = new PaymentInfoModel();
		PaymentInfoModel paymentInfoModel2 = paymentInfoModel;
		paymentInfoModel2.CardholderName = await _customerService.GetCustomerFullNameAsync(customer);
		paymentInfoModel.DisableBilling = _orderSettings.DisableBillingAddressCheckoutStep;
		PaymentInfoModel paymentInfoModel3 = paymentInfoModel;
		paymentInfoModel3.BillingAddress = await _addressService.GetAddressByIdAsync(customer.BillingAddressId.GetValueOrDefault());
		paymentInfoModel.PublishableApiKey = _stripe3DSecurePaymentSettings.PublishableApiKey;
		paymentInfoModel.PassBillingAddress = _stripe3DSecurePaymentSettings.PassBillingAddress;
		PaymentInfoModel model = paymentInfoModel;
		if (!(await _customerService.IsGuestAsync(customer, true)))
		{
			IShoppingCartService shoppingCartService = _shoppingCartService;
			Customer val = customer;
			IList<ShoppingCartItem> list = await shoppingCartService.GetShoppingCartAsync(val, (ShoppingCartType)1, ((BaseEntity)(await _storeContext.GetCurrentStoreAsync())).Id, (int?)null, (DateTime?)null, (DateTime?)null, (int?)null);
			paymentInfoModel = model;
			paymentInfoModel.IsRecurringOrder = await _shoppingCartService.ShoppingCartIsRecurringAsync(list);
		}
		return ((ViewComponent)this).View("~/Plugins/Payments.Stripe3DSecure/Views/PaymentInfo.cshtml", model);
	}
}
