using System.Collections.Generic;

namespace Nop.Plugin.Payments.Stripe3DSecure.Domain;

public class Stripe3DSecurePaymentResponce
{
	public string PaymentIntentId { get; set; }

	public string ClientSecret { get; set; }

	public IList<string> Warnings { get; set; }

	public Stripe3DSecurePaymentResponce()
	{
		Warnings = new List<string>();
	}
}
