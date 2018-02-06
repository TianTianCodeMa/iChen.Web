using System;
using System.Data.Odbc;
using iChen.Analytics;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace iChen.Web
{
	public enum DataFileFormats
	{
		JSON, CSV, TSV, XLS, XLSX
	}

	internal static class WebSettings
	{
		public static LogLevel LoggerLevel = LogLevel.Warning;
		public static string DatabaseSchema = null;
		public static ushort DatabaseVersion = 1;
		public static string WwwRootPath = @".\www\";
		public static string TerminalConfigFilePath = null;

		public const string Route_Logs = "logs";
		public const string Route_Config = "config";
		public const string Route_Reports = "reports";
	}

	internal static class Utils
	{
		public static void ProcessDateTimeRange (ref DateTimeOffset? from, ref DateTimeOffset? to)
		{
			// Default to MTD
			if (!to.HasValue) to = DateTimeOffset.Now;
			if (!from.HasValue) from = new DateTimeOffset(to.Value.Year, to.Value.Month, 1, 0, 0, 0, to.Value.Offset);
			if (from.Value > to.Value) throw new ArgumentOutOfRangeException("Start date must not be later than the end date.");
		}

		public static Sorting GetSorting (string sort)
		{
			if (string.IsNullOrWhiteSpace(sort)) return Sorting.None;

			switch (sort.ToLowerInvariant()) {
				case "time": return Sorting.ByTime;
				case "controller": return Sorting.ByController;
				default: return Sorting.None;
			}
		}
	}
}

namespace iChen.Web.Analytics
{
	internal class KeyEqualsPredicate<T> : IPredicate<T>
	{
		public string DatabaseKeyName { get; }
		public bool DatabaseKeyIsUnicode { get; }
		public string MatchValue { get; }
		public bool IgnoreCase { get; }

		public KeyEqualsPredicate (string value, string dbkey, bool unicode = false, bool ignoreCase = true)
		{
			if (value == null) throw new ArgumentNullException(nameof(value));
			if (string.IsNullOrWhiteSpace(dbkey)) throw new ArgumentNullException(nameof(dbkey));

			DatabaseKeyName = dbkey;
			DatabaseKeyIsUnicode = unicode;
			MatchValue = value;
			IgnoreCase = ignoreCase;
		}

		public Func<DynamicTableEntity, bool> GetLinqFilter ()
		{
			return entity => {
				if (!entity.Properties.ContainsKey(DatabaseKeyName)) return false;

				var keyval = entity.Properties[DatabaseKeyName].StringValue;
				if (string.IsNullOrWhiteSpace(keyval)) return false;

				return IgnoreCase ? MatchValue.Equals(keyval, StringComparison.InvariantCultureIgnoreCase) : MatchValue == keyval;
			};
		}

		public string GetSqlWhereClause ()
		{
			return $"{DatabaseKeyName}=?";
		}

		public void AddSqlParameters (OdbcParameterCollection parameters)
		{
			parameters.Add($"@{DatabaseKeyName}", DatabaseKeyIsUnicode ? OdbcType.NVarChar : OdbcType.VarChar).Value = MatchValue;
		}
	}
}