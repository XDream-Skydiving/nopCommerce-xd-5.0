using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Stripe3DSecure.Infrastructure;

public class RouteProvider : IRouteProvider
{
	public int Priority => -1;

	public void RegisterRoutes(IEndpointRouteBuilder routeBuilder)
	{
		routeBuilder.MapControllerRoute(Stripe3DSecurePaymentDefaults.WebhookRouteName, Stripe3DSecurePaymentDefaults.WebhookRouteUrl, new
		{
			controller = "Stripe3DSecureWebhook",
			action = "WebhookEventsHandler"
		});
	}
}
