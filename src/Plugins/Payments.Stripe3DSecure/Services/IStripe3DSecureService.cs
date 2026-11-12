using System.Threading.Tasks;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Payments.Stripe3DSecure.Services;

public interface IStripe3DSecureService
{
	Task<int> CheckLicenseKeyValid(string licenseKey);

	Task<Order> GetOrderBySubscriptionTransactionIdAndPaymentMethodAsync(string subscriptionTransactionId, string paymentMethodSystemName = null);

	Task<Order> GetOrderByCaptureTransactionIdAsync(string captureTransactionId);
}
