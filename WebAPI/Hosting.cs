﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace iChen.Web
{
	public static class Hosting
	{
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
					if (m_OS == null) {
						var name = (from x in new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem").Get().Cast<ManagementObject>()
												select x.GetPropertyValue("Caption")).FirstOrDefault();
						m_OS = name?.ToString() ?? "Unknown";
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

		public static readonly Status CurrentStatus = new Status();

		// Web host
		private static IWebHost m_Host = null;

		public static IWebHost CreateWebHost (string DebugLevel, string DatabaseSchema = null, ushort DatabaseVersion = 1, string IPandPort = "http://localhost:5757", string WwwRoot = @"./www", string TerminalConfigFile = @"./www/terminal/config.js", string LogsPath = @"./logs", int SessionTimeOut = 15)
		{
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
			if (SessionTimeOut > 0) Sessions.TimeOut = SessionTimeOut * 60 * 1000;

			var builder = new WebHostBuilder()
												.UseKestrel()
												.UseWebRoot(WebSettings.WwwRootPath)
												.UseStartup<Startup>()
												.UseUrls(IPandPort);

			switch (DebugLevel.ToUpperInvariant()) {
				case "TRACE": WebSettings.LoggerLevel = LogLevel.Trace; break;
				case "DEBUG": WebSettings.LoggerLevel = LogLevel.Debug; break;
				case "INFO": WebSettings.LoggerLevel = LogLevel.Information; break;
				case "WARN": WebSettings.LoggerLevel = LogLevel.Warning; break;
				case "ERROR": WebSettings.LoggerLevel = LogLevel.Error; break;
				case "FATAL": WebSettings.LoggerLevel = LogLevel.Critical; break;
				case "NONE": WebSettings.LoggerLevel = LogLevel.None; break;
				default: throw new ArgumentOutOfRangeException(nameof(DebugLevel), $"Invalid debug level: [{DebugLevel}].");
			}

			if (WebSettings.LoggerLevel != LogLevel.None) {
				builder = builder.ConfigureLogging(logging => {
					logging.SetMinimumLevel(WebSettings.LoggerLevel);
					logging.AddConsole();
				});
			}

			return builder.Build();
		}

		/// <summary>Start self-hosted website and Web API interface</summary>
		/// <param name="DebugLevel">Console logger minimum level.</param>
		/// <param name="DatabaseSchema">Specific schema (if any) of the database; default "dbo".</param>
		/// <param name="DatabaseVersion">Specific version of the database; default 1.</param>
		/// <param name="IPandPort">Website IP and port number.</param>
		/// <param name="WwwRoot">Root folder of the website.</param>
		/// <param name="TerminalConfigFile">Path to the iChen Terminal's config file (config.js)</param>
		/// <param name="LogsPath">Path to the logs folders.</param>
		/// <param name="SessionTimeOut">Period of inactivity to invalidate a login session (in minutes).</param>
		public static void RunWebHost (string DebugLevel, string DatabaseSchema = null, ushort DatabaseVersion = 1, string IPandPort = "http://localhost:5757", string WwwRoot = @"./www", string TerminalConfigFile = @"./www/terminal/config.js", string LogsPath = @"./logs", int SessionTimeOut = 15)
		{
			if (m_Host != null) throw new ApplicationException("Web host is already started.");

			if (DatabaseSchema != null && string.IsNullOrWhiteSpace(DatabaseSchema)) throw new ArgumentNullException(nameof(DatabaseSchema));
			if (DatabaseVersion <= 0) throw new ArgumentOutOfRangeException(nameof(DatabaseVersion));
			if (string.IsNullOrWhiteSpace(DebugLevel)) throw new ArgumentOutOfRangeException(nameof(DebugLevel));
			if (string.IsNullOrWhiteSpace(IPandPort)) throw new ArgumentOutOfRangeException(nameof(IPandPort));
			if (string.IsNullOrWhiteSpace(WwwRoot)) throw new ArgumentOutOfRangeException(nameof(WwwRoot));
			if (string.IsNullOrWhiteSpace(TerminalConfigFile)) throw new ArgumentOutOfRangeException(nameof(TerminalConfigFile));
			if (string.IsNullOrWhiteSpace(LogsPath)) throw new ArgumentOutOfRangeException(nameof(LogsPath));

			m_Host = CreateWebHost(DebugLevel, DatabaseSchema, DatabaseVersion, IPandPort, WwwRoot, TerminalConfigFile, LogsPath, SessionTimeOut);

			m_Host.Start();
		}

		public static void RunAzureWebHost (string DebugLevel, string StorageAccount, string StorageKey, string DatabaseSchema = null, ushort DatabaseVersion = 1, string IPandPort = "http://localhost:5757", string WwwRoot = @"./www", string TerminalConfigFile = @"./www/terminal/config.js", int SessionTimeOut = 15)
		{
			if (string.IsNullOrWhiteSpace(StorageAccount)) throw new ArgumentOutOfRangeException(nameof(StorageAccount));
			if (string.IsNullOrWhiteSpace(StorageKey)) throw new ArgumentOutOfRangeException(nameof(StorageKey));

			m_Host = CreateWebHost(DebugLevel, DatabaseSchema, DatabaseVersion, IPandPort, WwwRoot, TerminalConfigFile, "NO_LOGS_PATH", SessionTimeOut);

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