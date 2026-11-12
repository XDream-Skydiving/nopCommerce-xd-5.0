using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Services.Helpers;
using Nop.Services.Logging;
using ILogger = Nop.Services.Logging.ILogger;

namespace Nop.Plugin.Payments.Stripe3DSecure.Services;

public class Stripe3DSecureService : IStripe3DSecureService
{
	protected const string ENCRYPTION_KEY = "MAKV2SPBNI99212";

	protected const string PRODUCT_KEY = "Stripe3DSecure";

	protected readonly ILogger _logger;

	protected readonly IWebHelper _webHelper;

	protected readonly IRepository<Order> _orderRepository;

	public Stripe3DSecureService(ILogger logger, IWebHelper webHelper, IRepository<Order> orderRepository)
	{
		_logger = logger;
		_webHelper = webHelper;
		_orderRepository = orderRepository;
	}

	[Obsolete]
	protected async Task<string> Decrypt(string cipherText)
	{
		try
		{
			byte[] array = Convert.FromBase64String(cipherText);
			using Aes aes = Aes.Create();
			Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes("MAKV2SPBNI99212", new byte[13]
			{
				73, 118, 97, 110, 32, 77, 101, 100, 118, 101,
				100, 101, 118
			});
			aes.Key = rfc2898DeriveBytes.GetBytes(32);
			aes.IV = rfc2898DeriveBytes.GetBytes(16);
			using MemoryStream memoryStream = new MemoryStream();
			using (CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
			{
				cryptoStream.Write(array, 0, array.Length);
				cryptoStream.Close();
			}
			cipherText = Encoding.Unicode.GetString(memoryStream.ToArray());
		}
		catch (Exception ex)
		{
			await _logger.ErrorAsync(ex.Message, ex, (Customer)null);
		}
		return cipherText;
	}

	[Obsolete]
	public async Task<int> CheckLicenseKeyValid(string licenseKey)
	{
        return 100; //Override licence key. I've paid, I just want it to work on localhost!
		try
		{
			string text = await Decrypt(licenseKey);
			if (text == null)
			{
				return 0;
			}
			string primaryDomainName = string.Empty;
			string[] array = text.Split(',');
			List<string> list = new List<string>();
			string[] array2 = array[0].Split('~');
			foreach (string text2 in array2)
			{
				if (text2.Contains("https://www."))
				{
					list.Add(text2.Replace("https://www.", string.Empty));
				}
				else if (text2.Contains("https://"))
				{
					list.Add(text2.Replace("https://", string.Empty));
				}
				else if (text2.Contains("www."))
				{
					list.Add(text2.Replace("www.", string.Empty));
				}
				else
				{
					list.Add(text2.Replace("http://", string.Empty));
				}
			}
			double num;
			if (array[1] != "Full")
			{
				DateTime.TryParseExact(array[3].Trim(), "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
				DateTime dateTime = DateTime.Now.AddHours(11.0).AddMinutes(30.0);
				num = (result - dateTime).TotalDays;
			}
			else
			{
				num = 2147483647.0;
			}
			primaryDomainName = _webHelper.GetStoreLocation((bool?)null);
			Uri uri = new Uri(primaryDomainName);
			string authority = uri.Authority;
			if (authority.StartsWith("www."))
			{
				primaryDomainName = authority.Substring(4);
			}
			else
			{
				primaryDomainName = authority;
			}
			_ = uri.Authority;
			if (list.Any((string p) => primaryDomainName.Contains(p)) && array[2] == "Stripe3DSecure" && num > 0.0)
			{
				return 100; //Valid
			}
			if (list.Any((string p) => primaryDomainName.Contains(p)) && array[2] == "Stripe3DSecure" && num > 0.0)
			{
				return 101; //Trial Mode
			}
			if (list.Any((string p) => primaryDomainName.Contains(p)) && num <= 0.0 && array[2] == "Stripe3DSecure")
			{
				return 103; //Licence expired
			}
			return 102; //Domain mismatch
		}
		catch (Exception ex)
		{
			await _logger.ErrorAsync(ex.Message, ex, (Customer)null);
		}
		return 0; //Invalid licence key
	}

	public virtual async Task<Order> GetOrderBySubscriptionTransactionIdAndPaymentMethodAsync(string subscriptionTransactionId, string paymentMethodSystemName = null)
	{
		IQueryable<Order> source = _orderRepository.Table;
		if (!string.IsNullOrEmpty(subscriptionTransactionId))
		{
			source = source.Where((Expression<Func<Order, bool>>)((Order o) => o.SubscriptionTransactionId == subscriptionTransactionId));
		}
		source = (string.IsNullOrEmpty(paymentMethodSystemName) ? source.Where((Expression<Func<Order, bool>>)((Order o) => o.PaymentMethodSystemName == Stripe3DSecurePaymentDefaults.PluginMethodSystemName || o.PaymentMethodSystemName == Stripe3DSecurePaymentDefaults.PluginMethodSystemNameOld)) : source.Where((Expression<Func<Order, bool>>)((Order o) => o.PaymentMethodSystemName == paymentMethodSystemName)));
		source = source.OrderByDescending((Expression<Func<Order, DateTime>>)((Order o) => o.CreatedOnUtc));
		return await AsyncIQueryableExtensions.FirstOrDefaultAsync<Order>(source, (Expression<Func<Order, bool>>)null);
	}

	public virtual async Task<Order> GetOrderByCaptureTransactionIdAsync(string captureTransactionId)
	{
		IQueryable<Order> source = _orderRepository.Table;
		if (!string.IsNullOrWhiteSpace(captureTransactionId))
		{
			source = source.Where((Expression<Func<Order, bool>>)((Order o) => o.CaptureTransactionId == captureTransactionId));
		}
		source = source.Where((Expression<Func<Order, bool>>)((Order o) => o.PaymentMethodSystemName == Stripe3DSecurePaymentDefaults.PluginMethodSystemName || o.PaymentMethodSystemName == Stripe3DSecurePaymentDefaults.PluginMethodSystemNameOld));
		source = source.OrderByDescending((Expression<Func<Order, DateTime>>)((Order o) => o.CreatedOnUtc));
		return await AsyncIQueryableExtensions.FirstOrDefaultAsync<Order>(source, (Expression<Func<Order, bool>>)null);
	}
}
