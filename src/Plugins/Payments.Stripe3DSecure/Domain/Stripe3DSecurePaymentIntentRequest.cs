using System;

namespace Nop.Plugin.Payments.Stripe3DSecure.Domain;

public class Stripe3DSecurePaymentIntentRequest
{
	public string PaymentIntentId { get; set; }

	public Guid OrderGuid { get; set; }

	public long OrderTotal { get; set; }

	public string CurrencyCode { get; set; }

	public string Email { get; set; }

	public string CaptureMethod { get; set; }

	public bool IsRecurring { get; set; }

	public int CustomerId { get; set; }
}
