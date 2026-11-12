using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Stripe3DSecure;

public class Stripe3DSecurePaymentSettings : ISettings
{
	public TransactMode TransactMode { get; set; }

	public bool AdditionalFeePercentage { get; set; }

	public decimal AdditionalFee { get; set; }

	public string SecretApiKey { get; set; }

	public string PublishableApiKey { get; set; }

	public string WebhookId { get; set; }

	public string SigningSecretKey { get; set; }

	public string StatementDescriptor { get; set; }

	public bool SentReceiptEmail { get; set; }

	public bool PassBillingAddress { get; set; }

	public bool PassShippingAddress { get; set; }

	public string LicenseKey { get; set; }
}
