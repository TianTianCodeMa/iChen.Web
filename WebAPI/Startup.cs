using System.Collections.Generic;
using System.Linq;
using iChen.OpenProtocol;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace iChen.Web
{
	class Startup
	{
		public Startup (IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		private static readonly IEnumerable<string> CompressMimeTypes = new[]
		{
			// Text files
			"text/csv",

			// Fonts
			"font/truetype",
			"font/opentype",
			"font/woff",
			"font/woff2",
			"font/eot",
			"application/octet-stream",
			"application/x-font-truetype",
			"application/x-font-opentype",
			"application/font-woff",
			"application/font-woff2",
			"application/vnd.ms-fontobject",
			"application/font-sfnt",
			"image/svg+xml",
			"application/atom+xml",

			// Excel spreadsheets
			"application/vnd.ms-excel",
			"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
		};

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices (IServiceCollection services)
		{
			services.AddDbContext<ConfigDB>();

			services.Configure<GzipCompressionProviderOptions>(opt =>
				opt.Level = System.IO.Compression.CompressionLevel.Optimal);

			services.AddResponseCompression(opt => {
				opt.EnableForHttps = true;
				opt.Providers.Add<GzipCompressionProvider>();
				opt.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(CompressMimeTypes);
			});

			services.AddMvc().AddJsonOptions(options => {
				// Output formatters
				options.SerializerSettings.ContractResolver = new JsonSerializationContractResolver();
				options.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore;
				//options.SerializerSettings.DateFormatString = "yyyy-MM-ddTHH:mm:sszzz";
				options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
				options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
				options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
				options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
				options.SerializerSettings.Formatting = Formatting.None;
				options.SerializerSettings.TypeNameHandling = TypeNameHandling.None;
				options.SerializerSettings.Converters.Add(new StringEnumConverter());
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure (
			IApplicationBuilder app
			, IHostingEnvironment env
			, ILoggerFactory loggerFactory)
		{
			app.UseMiddleware<ExceptionCatchMiddleware>();

			if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

			app.UseResponseCompression();

			// Rewrite all URL's which is not referring to an actual file (i.e. with an extension
			// to the index.html page under the webapp
			app.UseRewriter(new RewriteOptions()
				.AddRewrite($"^{WebSettings.Route_Config}/(.*)$", $"{WebSettings.Route_Config}/$1", true)
				.AddRewrite($"^{WebSettings.Route_Logs}/(.*)$", $"{WebSettings.Route_Logs}/$1", true)
				.AddRewrite(@"^(\w+)/[^\.\/]+$", "$1/index.html", false)
			);

			app.UseDefaultFiles();

			app.UseStaticFiles();

			app.UseMvc();
		}
	}
}
