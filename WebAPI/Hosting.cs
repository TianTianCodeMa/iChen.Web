using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using iChen.OpenProtocol;
using iChen.Persistence.Server;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace iChen.Web
{
	public static partial class Hosting
	{
		public const string SessionCookieName = "api_key";

		#region Status class

		public class Status
		{
			private string m_OS = null;

			public DateTime Started { get; set; } = DateTime.MinValue;
			public TimeSpan? Uptime { get { return (Started != DateTime.MinValue) ? TimeSpan.FromSeconds(Math.Ceiling((DateTime.Now - Started).TotalSeconds)) : (TimeSpan?) null; } }
			public bool IsRunning { get; set; }
			public string Version { get; set; }

			public string Environment
			{
				get {
					if (m_OS != null) return m_OS;

					try {
						if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
							try {
								var name = (from x in new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem").Get().Cast<ManagementObject>()
														select x.GetPropertyValue("Caption")).FirstOrDefault();
								m_OS = name?.ToString() ?? "Unknown Windows";
							} catch {
								m_OS = "Unknown Windows";
							}
						} else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
							try {
								m_OS = System.Environment.OSVersion.ToString();
							} catch {
								m_OS = "Unknown Linux";
							}
						} else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
							m_OS = "OS/X";
						}
					} catch {
						m_OS = "Unknown";
					}

					return m_OS;
				}
			}

			public ushort? Port { get; set; }
			public ushort? OpenProtocol { get; set; }
			public IList<string> OPCUA { get; set; }
			public IDictionary<string, string> Controllers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			public IDictionary<string, string> Clients { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		#endregion

		#region MIME types for compression

		private static readonly IEnumerable<string> CompressMimeTypes = new[]
		{
			// Text files
			"text/plain",
			"text/csv",
			"text/html",
			"text/css",
			"application/javascript",
			"application/x-javascript",
			"text/javascript",

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

		#endregion

		public static readonly Status CurrentStatus = new Status();

		// Web host
		private static IWebHost m_Host = null;

		private static IWebHost CreateWebHost (
																CustomConfiguration Configure
																, string DatabaseSchema
																, ushort DatabaseVersion
																, string HttpsCertificateFile
																, string HttpsCertificateHash
																, ushort HttpRedirectionPort
																, ushort Port
																, string WwwRoot
																, string TerminalConfigFile
																, string LogsPath
																, uint SessionTimeOut
		)
		{
			// Set parameters

			var basedir = AppDomain.CurrentDomain.BaseDirectory;
			if (!Path.IsPathRooted(WwwRoot)) WwwRoot = (new Uri(Path.Combine(basedir, WwwRoot))).LocalPath;
			if (!Path.IsPathRooted(LogsPath)) LogsPath = (new Uri(Path.Combine(basedir, LogsPath))).LocalPath;
			if (!Path.IsPathRooted(TerminalConfigFile)) TerminalConfigFile = (new Uri(Path.Combine(WwwRoot, TerminalConfigFile))).LocalPath;

			WebSettings.DatabaseSchema = DatabaseSchema;
			WebSettings.DatabaseVersion = DatabaseVersion;
			WebSettings.WwwRootPath = WwwRoot;
			WebSettings.TerminalConfigFilePath = TerminalConfigFile;
			LogController.LogsPath = LogsPath;
			LogController.LogsStorageAccount = LogController.LogsStorageKey = null;

			// Environment

			var isDevelopment = false; ;

			// HTTPS

			var useHttps = !string.IsNullOrWhiteSpace(HttpsCertificateFile) && !string.IsNullOrWhiteSpace(HttpsCertificateHash);
			var useHsts = useHttps;
			var useHttpRedirection = false;

			// Create Kestrel

			var builder = WebHost.CreateDefaultBuilder()
												.ConfigureAppConfiguration((context, _) => isDevelopment = context.HostingEnvironment.IsDevelopment())
												.UseKestrel(options => {
													if (useHttps) {
														options.ListenAnyIP(Port, listen => {
															listen.Protocols = HttpProtocols.Http1AndHttp2;
															listen.UseHttps(HttpsCertificateFile.Trim(), HttpsCertificateHash.Trim());
														});
														if (HttpRedirectionPort > 0) options.ListenAnyIP(HttpRedirectionPort);
													} else {
														options.ListenAnyIP(Port);
													}
												})
												.UseWebRoot(WwwRoot);

			if (useHttps && HttpRedirectionPort > 0) {
				builder.UseSetting("https_port", Port.ToString());      // HTTPS redirection port
				useHttpRedirection = true;
			}

			// Configure default logging
			builder.ConfigureLogging((ILoggingBuilder logging) => {
				logging.ClearProviders();

				if (Configure == null) {
					logging.SetMinimumLevel(WebSettings.DefaultLoggingLevel);
					logging.AddConsole();
				}
			});

			// Configure services

			builder.ConfigureServices((IServiceCollection services) => {
				services.AddDbContext<ConfigDB>();

				services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opt => {
					opt.Cookie.Name = SessionCookieName;
					opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
					opt.ExpireTimeSpan = TimeSpan.FromMilliseconds(SessionTimeOut * 60 * 1000);
					opt.SlidingExpiration = true;

					//SessionController.SessionsCache = new SessionStore(new MemoryCache(new MemoryCacheOptions()));
					//opt.SessionStore = SessionController.SessionsCache;

					opt.Events.OnRedirectToLogin = context => {
						context.Response.StatusCode = 401;
						return Task.CompletedTask;
					};
				});

				services.AddResponseCompression(opt => {
					opt.EnableForHttps = true;

					// Brotli compression is only implemented in .NET Core
					if (Utils.IsNetCore) opt.Providers.Add<BrotliCompressionProvider>();

					opt.Providers.Add<GzipCompressionProvider>();
					opt.MimeTypes = ResponseCompressionDefaults.MimeTypes.Union(CompressMimeTypes).ToList();
				}).Configure<BrotliCompressionProviderOptions>(opt => {
					opt.Level = CompressionLevel.Optimal;
				}).Configure<GzipCompressionProviderOptions>(opt => {
					opt.Level = CompressionLevel.Optimal;
				});

				services.AddMvc(opt => {
					var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
					opt.Filters.Add(new AuthorizeFilter(policy));
				}).AddJsonOptions(options => {
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
			});

			// Configure Pipeline

			builder.Configure(app => {
				var loggerFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();

				if (Configure != null) Configure(app, loggerFactory);

				app.UseMiddleware<ExceptionCatchMiddleware>();

				if (isDevelopment) app.UseDeveloperExceptionPage();

				if (useHsts) app.UseHsts();
				if (useHttpRedirection) app.UseHttpsRedirection();

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

				app.UseAuthentication();

				app.UseMvc();
			});

			// Build the web host

			return builder.Build();
		}
	}
}