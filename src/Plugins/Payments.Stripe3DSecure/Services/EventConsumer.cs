using System.Threading.Tasks;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Services.Events;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Events;

namespace Nop.Plugin.Payments.Stripe3DSecure.Services;

public class EventConsumer : IConsumer<PageRenderingEvent>, IConsumer<EntityUpdatedEvent<RecurringPayment>>
{
	protected readonly Stripe3DSecureManager _stripe3DSecureManager;

	protected readonly IPaymentPluginManager _paymentPluginManager;

	protected readonly IOrderService _orderService;

	protected readonly IOrderProcessingService _orderProcessingService;

	public EventConsumer(Stripe3DSecureManager stripe3DSecureManager, IPaymentPluginManager paymentPluginManager, IOrderService orderService, IOrderProcessingService orderProcessingService)
	{
		_stripe3DSecureManager = stripe3DSecureManager;
		_paymentPluginManager = paymentPluginManager;
		_orderService = orderService;
		_orderProcessingService = orderProcessingService;
	}

	public async Task HandleEventAsync(PageRenderingEvent eventMessage)
	{
		if (await _paymentPluginManager.IsPluginActiveAsync(Stripe3DSecurePaymentDefaults.PluginMethodSystemName, (Customer)null, 0))
		{
			string text = eventMessage.GetRouteName(false) ?? string.Empty;
			if (!string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(text) && (text.Equals("RealOnePageCheckout") || text.Equals("CheckoutOnePage") || text.Equals("CheckoutPaymentInfo")))
			{
				eventMessage.Helper.AddCssFileParts("~/Plugins/Payments.Stripe3DSecure/Content/css/paymentfont.min.css", "", true);
				eventMessage.Helper.AddCssFileParts("~/Plugins/Payments.Stripe3DSecure/Content/css/stripe.css", "", true);
			}
		}
	}

	public async Task HandleEventAsync(EntityUpdatedEvent<RecurringPayment> eventMessage)
	{
		RecurringPayment recurringPayment = eventMessage.Entity;
		if (recurringPayment != null)
		{
			Order initialOrder = await _orderService.GetOrderByIdAsync(recurringPayment.InitialOrderId);
			bool flag = initialOrder != null;
			if (flag)
			{
				flag = await _orderProcessingService.GetCyclesRemainingAsync(recurringPayment) == 0;
			}
			if (flag && !string.IsNullOrEmpty(initialOrder.SubscriptionTransactionId))
			{
				await _stripe3DSecureManager.SubscriptionCancelAtPeriodEndAsync(initialOrder.SubscriptionTransactionId);
			}
		}
	}
}
