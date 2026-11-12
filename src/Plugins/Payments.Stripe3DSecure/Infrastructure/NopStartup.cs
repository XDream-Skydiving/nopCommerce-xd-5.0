using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Stripe3DSecure.Services;

namespace Nop.Plugin.Payments.Stripe3DSecure.Infrastructure;

public class NopStartup : INopStartup
{
	public int Order => 3000;

	public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
	{
		services.Configure((KestrelServerOptions options) =>
		{
			options.AllowSynchronousIO = true;
		});
		services.Configure((IISServerOptions options) =>
		{
			options.AllowSynchronousIO = true;
		});
		services.AddScoped<Stripe3DSecureManager>();
		services.AddScoped<IStripe3DSecureService, Stripe3DSecureService>();
	}

	public void Configure(IApplicationBuilder application)
	{
	}
}
