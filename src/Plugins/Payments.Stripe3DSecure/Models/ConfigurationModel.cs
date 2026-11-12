using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Stripe3DSecure.Models;

public record ConfigurationModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    public int TransactModeId { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.TransactMode")]
    public IList<SelectListItem> TransactModeValues { get; set; } = new List<SelectListItem>();

    public bool TransactModeId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }

    public bool AdditionalFee_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }

    public bool AdditionalFeePercentage_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.PublishableApiKey")]
    [Required(ErrorMessage = "Plugin.Payments.Stripe3DSecure.Fields.PublishableApiKey.Required")]
    public string PublishableApiKey { get; set; } = string.Empty;

    public bool PublishableApiKey_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.SecretApiKey")]
    [Required(ErrorMessage = "Plugin.Payments.Stripe3DSecure.Fields.SecretApiKey.Required")]
    public string SecretApiKey { get; set; } = string.Empty;

    public bool SecretApiKey_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.StatementDescriptor")]
    [StringLength(22, ErrorMessage = "Plugin.Payments.Stripe3DSecure.Fields.StatementDescriptor.MaxLengthValidation")]
    public string StatementDescriptor { get; set; } = string.Empty;

    public bool StatementDescriptor_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.SentReceiptEmail")]
    public bool SentReceiptEmail { get; set; }

    public bool SentReceiptEmail_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.PassBillingAddress")]
    public bool PassBillingAddress { get; set; }

    public bool PassBillingAddress_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.PassShippingAddress")]
    public bool PassShippingAddress { get; set; }

    public bool PassShippingAddress_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.WebhookUrl")]
    public string WebhookUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.WebhookId")]
    public string WebhookId { get; set; } = string.Empty;

    public bool WebhookId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.SigningSecretKey")]
    public string SigningSecretKey { get; set; } = string.Empty;

    public bool SigningSecretKey_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugin.Payments.Stripe3DSecure.Fields.LicenseKey")]
    public string LicenseKey { get; set; } = string.Empty;
}
