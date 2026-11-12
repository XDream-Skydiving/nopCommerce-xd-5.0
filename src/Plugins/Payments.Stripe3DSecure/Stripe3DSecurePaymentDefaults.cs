using System.Collections.Generic;

namespace Nop.Plugin.Payments.Stripe3DSecure;

public static class Stripe3DSecurePaymentDefaults
{
	public static string PluginMethodSystemName = "Payments.Stripe3DSecure";

	public static string PluginMethodSystemNameOld = "Stripe.3DSecure";

	public static string PaymentIntentKey = "Stripe3DSecurePaymentIntentId";

	public static string PaymentMethodKey = "Stripe3DSecurePaymentMethodId";

	public static string StripeCustomerKey = "Stripe3DSecureCustomerId";

	public static string StripeProductKey = "Stripe3DSecureProductId";

	public static string StripeScriptPath = "https://js.stripe.com/v3/";

	public static string PaymentIntentSuccess = "payment_intent.succeeded";

	public static string PaymentIntentPaymentFailed = "payment_intent.payment_failed";

	public static string ChargeRefunded = "charge.refunded";

	public static string CustomerSubscriptionCreated = "customer.subscription.created";

	public static string CustomerSubscriptionDeleted = "customer.subscription.deleted";

	public static string PaymentIntentStatusRequiresAction = "requires_action";

	public static string PaymentIntentStatusSucceeded = "succeeded";

	public static string PaymentIntentStatusRequiresCapture = "requires_capture";

	public static string PaymentIntentStatusRequiresConfirmation = "requires_confirmation";

	public static string PaymentIntentStatusRequiresPaymentMethod = "requires_payment_method";

	public static string StripeSubscriptionStatusTraialing = "trialing";

	public static int StatementDescriptorLength => 22;

	public static string WebhookRouteName => "Plugin.Payments.Stripe3DSecure.Webhook";

	public static string WebhookRouteUrl => "Plugins/Stripe3DSecure/Webhook";

	public static List<string> WebhookEventNames => new List<string> { "payment_intent.succeeded", "payment_intent.payment_failed", "charge.refunded", "customer.subscription.created", "customer.subscription.deleted" };

	public static string[] ZeroDecimal => new string[16]
	{
		"BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF",
		"UGX", "VND", "VUV", "XAF", "XOF", "XPF"
	};

	public static string FixIllegalJavaScriptChars(this string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		text = text.Replace("'", "\\'");
		return text;
	}
}
