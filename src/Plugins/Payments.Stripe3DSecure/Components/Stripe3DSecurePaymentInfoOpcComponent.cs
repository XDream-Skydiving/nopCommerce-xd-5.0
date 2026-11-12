using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Stripe3DSecure.Models;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Stripe3DSecure.Components;

public class Stripe3DSecurePaymentInfoOpcComponent : NopViewComponent
{
	protected readonly IWorkContext _workContext;

	protected readonly ICustomerService _customerService;

	protected readonly OrderSettings _orderSettings;

	protected readonly Stripe3DSecurePaymentSettings _stripe3DSecurePaymentSettings;

	protected readonly IShoppingCartService _shoppingCartService;

	protected readonly IStoreContext _storeContext;

	protected readonly ICountryService _countryService;

	protected readonly IStateProvinceService _stateProvinceService;

	protected readonly IAddressService _addressService;

	public Stripe3DSecurePaymentInfoOpcComponent(IWorkContext workContext, ICustomerService customerService, OrderSettings orderSettings, Stripe3DSecurePaymentSettings stripe3DSecurePaymentSettings, IShoppingCartService shoppingCartService, IStoreContext storeContext, ICountryService countryService, IStateProvinceService stateProvinceService, IAddressService addressService)
	{
		_workContext = workContext;
		_customerService = customerService;
		_orderSettings = orderSettings;
		_stripe3DSecurePaymentSettings = stripe3DSecurePaymentSettings;
		_shoppingCartService = shoppingCartService;
		_storeContext = storeContext;
		_countryService = countryService;
		_stateProvinceService = stateProvinceService;
		_addressService = addressService;
	}

	public async Task<IViewComponentResult> InvokeAsync()
	{
		if (string.IsNullOrEmpty(_stripe3DSecurePaymentSettings.PublishableApiKey))
		{
			return ((ViewComponent)this).Content(string.Empty);
		}
		Customer customer = await _workContext.GetCurrentCustomerAsync();
		IShoppingCartService shoppingCartService = _shoppingCartService;
		Customer val = customer;
		IList<ShoppingCartItem> cart = await shoppingCartService.GetShoppingCartAsync(val, (ShoppingCartType)1, ((BaseEntity)(await _storeContext.GetCurrentStoreAsync())).Id, (int?)null, (DateTime?)null, (DateTime?)null, (int?)null);
		Address billingAddress = await _addressService.GetAddressByIdAsync(customer.BillingAddressId.GetValueOrDefault());
		Address shippingAddress = await _addressService.GetAddressByIdAsync(customer.ShippingAddressId.GetValueOrDefault());
		PaymentInfoModel paymentInfoModel = new PaymentInfoModel();
		PaymentInfoModel paymentInfoModel2 = paymentInfoModel;
		paymentInfoModel2.CardholderName = await _customerService.GetCustomerFullNameAsync(customer);
		paymentInfoModel.DisableBilling = _orderSettings.DisableBillingAddressCheckoutStep;
		paymentInfoModel.PublishableApiKey = _stripe3DSecurePaymentSettings.PublishableApiKey;
		paymentInfoModel.PassBillingAddress = _stripe3DSecurePaymentSettings.PassBillingAddress;
		PaymentInfoModel paymentInfoModel3 = paymentInfoModel;
		object obj = ((billingAddress != null) ? billingAddress.Email : null);
		if (obj == null)
		{
			obj = ((shippingAddress != null) ? shippingAddress.Email : null) ?? customer.Email ?? string.Empty;
		}
		paymentInfoModel3.ReceiptEmail = (string)obj;
		PaymentInfoModel paymentInfoModel4 = paymentInfoModel;
		paymentInfoModel4.IsRecurring = await _shoppingCartService.ShoppingCartIsRecurringAsync(cart);
		PaymentInfoModel paymentInfoModel5 = paymentInfoModel;
		paymentInfoModel5.IsRequiresShipping = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
		paymentInfoModel.SentReceiptEmail = _stripe3DSecurePaymentSettings.SentReceiptEmail;
		paymentInfoModel.PassShippingAddress = _stripe3DSecurePaymentSettings.PassShippingAddress;
		PaymentInfoModel model = paymentInfoModel;
		if (!(await _customerService.IsGuestAsync(customer, true)))
		{
			paymentInfoModel = model;
			paymentInfoModel.IsRecurringOrder = await _shoppingCartService.ShoppingCartIsRecurringAsync(cart);
		}
		ViewDataDictionary viewData = ((ViewComponent)this).ViewData;
		viewData["Countries"] = (await _countryService.GetAllCountriesAsync(0, false)).Select((Country p) => new
		{
			((BaseEntity)p).Id,
			p.TwoLetterIsoCode
		});
		viewData = ((ViewComponent)this).ViewData;
		viewData["Provinces"] = (await _stateProvinceService.GetStateProvincesAsync(false)).Select((StateProvince p) => new
		{
			((BaseEntity)p).Id,
			p.Abbreviation
		});
		return ((ViewComponent)this).View("~/Plugins/Payments.Stripe3DSecure/Views/PaymentInfoOpc.cshtml", model);
	}
}
