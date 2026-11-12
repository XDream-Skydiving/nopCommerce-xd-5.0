using Nop.Core.Domain.Common;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.Stripe3DSecure.Models;

public record PaymentInfoModel : BaseNopModel
{
    public string CardholderName { get; set; } = string.Empty;

    public string CardNonce { get; set; } = string.Empty;

    public Address BillingAddress { get; set; }

    public bool DisableBilling { get; set; }

    public string PublishableApiKey { get; set; } = string.Empty;

    public bool PassBillingAddress { get; set; }

    public bool IsRecurringOrder { get; set; }

    public string ReceiptEmail { get; set; } = string.Empty;

    public bool IsRecurring { get; set; }

    public bool IsRequiresShipping { get; set; }

    public bool SentReceiptEmail { get; set; }

    public bool PassShippingAddress { get; set; }
}
