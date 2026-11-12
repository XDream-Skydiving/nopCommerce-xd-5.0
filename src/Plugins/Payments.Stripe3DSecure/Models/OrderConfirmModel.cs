using Nop.Core.Domain.Common;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.Stripe3DSecure.Models;

public record OrderConfirmModel : BaseNopModel
{
    public string PublishableApiKey { get; set; } = string.Empty;

    public string PaymentMethodId { get; set; } = string.Empty;

    public string ReceiptEmail { get; set; } = string.Empty;

    public Address ShippingAddress { get; set; }

    public bool IsRecurring { get; set; }

    public bool IsRequiresShipping { get; set; }

    public bool SentReceiptEmail { get; set; }

    public bool PassShippingAddress { get; set; }
}
