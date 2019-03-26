using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace iChen.Web
{
	public delegate void CustomConfiguration (IApplicationBuilder app, ILoggerFactory loggerFactory);

	public static partial class Hosting
	{
		public static void RunWebHost (
											CustomConfiguration Configure
											, string DatabaseSchema = null
											, ushort DatabaseVersion = 1
											, string HttpsCertificateFile = null
											, string HttpsCertificateHash = null
											, ushort HttpRedirectionPort = 0
											, ushort Port = 5757
											, string WwwRoot = @"./www"
											, string TerminalConfigFile = @"./www/terminal/config.js"
											, string LogsPath = @"./logs"
											, uint SessionTimeOut = 15
)
		{
			if (m_Host != null) throw new ApplicationException("Web host is already started.");

			if (DatabaseSchema != null && string.IsNullOrWhiteSpace(DatabaseSchema)) throw new ArgumentNullException(nameof(DatabaseSchema));
			if (DatabaseVersion <= 0) throw new ArgumentOutOfRangeException(nameof(DatabaseVersion));
			if (string.IsNullOrWhiteSpace(WwwRoot)) throw new ArgumentOutOfRangeException(nameof(WwwRoot));
			if (string.IsNullOrWhiteSpace(TerminalConfigFile)) throw new ArgumentOutOfRangeException(nameof(TerminalConfigFile));
			if (string.IsNullOrWhiteSpace(LogsPath)) throw new ArgumentOutOfRangeException(nameof(LogsPath));
			if (Port <= 0) throw new ArgumentOutOfRangeException(nameof(Port));
			if (SessionTimeOut <= 0) throw new ArgumentOutOfRangeException(nameof(SessionTimeOut));
			if (!string.IsNullOrWhiteSpace(HttpsCertificateFile) && string.IsNullOrWhiteSpace(HttpsCertificateHash)) throw new ArgumentNullException(nameof(HttpsCertificateHash));

			m_Host = CreateWebHost(Configure, DatabaseSchema, DatabaseVersion, HttpsCertificateFile, HttpsCertificateHash, HttpRedirectionPort, Port, WwwRoot, TerminalConfigFile, LogsPath, SessionTimeOut);

			m_Host.Start();
		}

		public static void RunAzureWebHost (
													CustomConfiguration Configure
													, string StorageAccount
													, string StorageKey
													, string DatabaseSchema = null
													, ushort DatabaseVersion = 1
													, string HttpsCertificateFile = null
													, string HttpsCertificateHash = null
													, ushort HttpRedirectionPort = 0
													, ushort Port = 5757
													, string WwwRoot = @"./www"
													, string TerminalConfigFile = @"./www/terminal/config.js"
													, uint SessionTimeOut = 15
		)
		{
			if (m_Host != null) throw new ApplicationException("Web host is already started.");

			if (string.IsNullOrWhiteSpace(StorageAccount)) throw new ArgumentOutOfRangeException(nameof(StorageAccount));
			if (string.IsNullOrWhiteSpace(StorageKey)) throw new ArgumentOutOfRangeException(nameof(StorageKey));
			if (DatabaseSchema != null && string.IsNullOrWhiteSpace(DatabaseSchema)) throw new ArgumentNullException(nameof(DatabaseSchema));
			if (DatabaseVersion <= 0) throw new ArgumentOutOfRangeException(nameof(DatabaseVersion));
			if (Port <= 0) throw new ArgumentOutOfRangeException(nameof(Port));
			if (SessionTimeOut <= 0) throw new ArgumentOutOfRangeException(nameof(SessionTimeOut));
			if (string.IsNullOrWhiteSpace(WwwRoot)) throw new ArgumentOutOfRangeException(nameof(WwwRoot));
			if (string.IsNullOrWhiteSpace(TerminalConfigFile)) throw new ArgumentOutOfRangeException(nameof(TerminalConfigFile));
			if (!string.IsNullOrWhiteSpace(HttpsCertificateFile) && string.IsNullOrWhiteSpace(HttpsCertificateHash)) throw new ArgumentNullException(nameof(HttpsCertificateHash));

			m_Host = CreateWebHost(Configure, DatabaseSchema, DatabaseVersion, HttpsCertificateFile, HttpsCertificateHash, HttpRedirectionPort, Port, WwwRoot, TerminalConfigFile, "NO_LOGS_PATH", SessionTimeOut);

			LogController.LogsStorageAccount = StorageAccount.Trim();
			LogController.LogsStorageKey = StorageKey.Trim();
			LogController.LogsPath = null;

			m_Host.Start();
		}

		public static void StopWebHost ()
		{
			if (m_Host == null) throw new ApplicationException("Web host is not yet started.");

			m_Host.Dispose();
			m_Host = null;
		}
	}
}
