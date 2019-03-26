using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using iChen.OpenProtocol;
using iChen.Persistence.Cloud;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace iChen.Web
{
	[Route(WebSettings.Route_Logs)]
	public partial class LogController : Microsoft.AspNetCore.Mvc.Controller
	{
		public const int LogsStorageDaysInterval = 30;

		public static string LogsPath = "~/logs";
		public static string LogsStorageAccount = "ichen";
		public static string LogsStorageKey = null;
		public static string LogsStorageTable = "ServerLogs";

		private static readonly string[] RemoveClassPrefixList = new[] { "iChenServer.Services.", "iChenServer." };

		private static readonly Regex LogFileRegex = new Regex(@".+_(?<date>\d{8})\.log$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
		private static readonly Regex LogLineRegex = new Regex(@"^(?<date>\d{4}-\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}:\d{2},\d{3})\s+(?<level>\w+)\s+\[(?<thread>\w+)\]\s+\((?<class>[\w\.\-]+)\)\s+\-\s+(?<message>.*)$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		private bool UseAzureStorage { get { return LogsStorageAccount != null && LogsStorageKey != null; } }
		private bool UseLogFiles { get { return LogsPath != null; } }

		#region Data classes

		internal class LogEntity : TableEntity
		{
			public string LoggerName { get; set; }
			public string Level { get; set; }
			public DateTime EventTimeStamp { get; set; }
			public string ThreadName { get; set; }
			public string Message { get; set; }
		}

		public class LogEntry
		{
			public DateTimeOffset Time { get; set; }
			public string Level { get; set; }
			public string Class { get; set; }
			public string Thread { get; set; }
			public string Message { get; set; }

			public static readonly string[] HeaderNames = new[] { nameof(Time), nameof(Level), nameof(Class), nameof(Thread), nameof(Thread) };

			public LogEntry ()
			{
			}

			internal LogEntry (LogEntity entity)
			{
				if (entity == null) throw new ArgumentNullException(nameof(entity));

				Time = new DateTimeOffset(entity.EventTimeStamp.Ticks, DateTimeOffset.Now.Offset);
				Level = entity.Level;
				Class = entity.LoggerName.Substring(RemoveClassPrefixList.FirstOrDefault(prefix => entity.LoggerName.StartsWith(prefix))?.Length ?? 0);
				Thread = entity.ThreadName;
				Message = entity.Message.TrimEnd('\r', '\n').TrimEnd();
			}

			public string ToCSVDataLine (string separator = ",", bool quoted = true)
				=> $@"{Time.ToString("o")}{separator}{Level}{separator}{Class}{separator}{Thread}{separator}{(quoted ? "\"" : "")}{(quoted ? Message.Replace("\"", "\"\"") : Message)}{(quoted ? "\"" : "")}";

			public void FillXlsRow (IWorkbook workbook, ISheet sheet, IRow row, ICellStyle datestyle)
			{
				var cell = row.CreateCell(0);
				cell.SetCellValue(Time.DateTime);
				cell.CellStyle = datestyle;

				row.CreateCell(1, CellType.String).SetCellValue(Level);
				row.CreateCell(2, CellType.String).SetCellValue(Class);
				row.CreateCell(3, CellType.String).SetCellValue(Thread);
				row.CreateCell(4, CellType.String).SetCellValue(Message);
			}
		}

		#endregion Data classes

		private CloudTable ConnectToStorage ()
		{
			var storage = CloudStorageAccount.Parse($"DefaultEndpointsProtocol=https;AccountName={LogsStorageAccount};AccountKey={LogsStorageKey}");
			var client = storage.CreateCloudTableClient();
			return client.GetTableReference(LogsStorageTable);
		}

		private async Task<(IActionResult result, IEnumerable<string> lines)> ReadLogFileAsync (string date, CancellationToken ct)
		{
			if (LogsPath == null) throw new ArgumentNullException(nameof(LogsPath));
			if (string.IsNullOrWhiteSpace(date)) return (BadRequest("Missing date."), null);

			if (!DateTime.TryParse(date.Trim(), out var dateval)) return (BadRequest($"Invalid date format: [{date}]."), null);

			var root = LogsPath;

			var file = Directory.GetFiles(root, "*.log")
									.Select(fn => LogFileRegex.Match(fn))
									.Where(match => match.Success)
									.Where(match => match.Groups["date"].Value == dateval.ToString("yyyyMMdd"))
									.Select(match => match.ToString())
									.FirstOrDefault();

			if (file == null) return (Ok(), null);

			var finfo = new FileInfo(file);
			var length = finfo.Length;
			string text = null;

			using (var stream = System.IO.File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
				using (var reader = new StreamReader(stream, Encoding.UTF8)) {
					text = await reader.ReadToEndAsync().ConfigureAwait(false);
				}
			}

			var lines = text.Split('\r', '\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

			return (Ok(lines), lines);
		}

		[HttpGet]
		[Authorize(Roles = nameof(Filters.All) + ", Org_" + DataStore.DefaultOrgId)]
		public IActionResult GetLogFilesList (int days = LogsStorageDaysInterval)
		{
			var now = DateTime.Now.Date;
			var start = now.AddDays(-days);

			if (UseAzureStorage) {
				var list = new List<string>();

				for (var date = start; date <= now; date = date.AddDays(1)) {
					list.Add($"{date.Year.ToString("0000")}-{date.Month.ToString("00")}-{date.Day.ToString("00")}");
				}

				return Ok(list.OrderByDescending(x => x).ToList());
			}

			if (UseLogFiles) {
				var root = LogsPath;

				var list = Directory.GetFiles(root, "*.log")
														.Select(fn => LogFileRegex.Match(fn))
														.Where(match => match.Success)
														.Select(match => match.Groups["date"].Value.Trim())
														.OrderByDescending(date => date)
														.Select(date => date.Substring(0, 4) + "-" + date.Substring(4, 2) + "-" + date.Substring(6, 2))
														.Where(date => DateTime.Parse(date) >= start)
														.ToList();

				return Ok(list);
			}

			return StatusCode(500);
		}

		[HttpGet("list")]
		public IActionResult GetLogFilesList2 (int days = LogsStorageDaysInterval) => GetLogFilesList(days);

		private static string BuildCSVFile (string[] headers, IEnumerable<LogEntry> data, string delimiter = ",", bool quoted = true)
		{
			var sb = new StringBuilder();
			sb.AppendLine(string.Join(delimiter, headers));

			foreach (var line in data.OrderBy(log => log.Time)) sb.AppendLine(line.ToCSVDataLine(delimiter, quoted));

			return sb.ToString();
		}

		private static void BuildXLSFile (Stream stream, IWorkbook xls, string sheet, IEnumerable<string> headers, IEnumerable<LogEntry> data)
		{
			var ss = xls.CreateSheet(sheet);

			var header = ss.CreateRow(0);
			var col = 0;

			foreach (var field in headers) {
				var cell = header.CreateCell(col++);
				cell.SetCellValue(field);
			}

			var dateformat = xls.CreateDataFormat().GetFormat("yyyy-MM-dd HH:mm:ss");
			var datestyle = xls.CreateCellStyle();
			datestyle.DataFormat = dateformat;

			var row = 1;

			foreach (var record in data.OrderBy(log => log.Time)) {
				var line = ss.CreateRow(row);
				record.FillXlsRow(xls, ss, line, datestyle);
				row++;
			}

			xls.Write(stream);
		}

		[HttpGet("{date}")]
		[Authorize(Roles = nameof(Filters.All) + ", Org_" + DataStore.DefaultOrgId)]
		public async Task<IActionResult> GetLogFileLines (string date, CancellationToken ct, string level = null, [Bind(Prefix = "class")] string classname = null, DataFileFormats format = DataFileFormats.JSON)
		{
			if (!string.IsNullOrWhiteSpace(classname)) classname = classname.Trim();
			var fullclassnames = RemoveClassPrefixList.Select(prefix => prefix + classname).ToList();

			IEnumerable<LogEntry> data = null;

			if (UseAzureStorage) {
				var cloud = ConnectToStorage();

				var startdate = DateTime.Parse(date);
				var enddate = startdate.AddDays(1);

				var maxdatetick = (DateTime.MaxValue.Ticks - startdate.Ticks + 1).ToString("D19");
				var mindatetick = (DateTime.MaxValue.Ticks - enddate.Ticks + 1).ToString("D19");

				/*
				var query = cloud.CreateQuery<LogEntity>().Where(log => log.PartitionKey.CompareTo(mindatetick) >= 0 && log.PartitionKey.CompareTo(maxdatetick) <= 0);
				if (!string.IsNullOrWhiteSpace(level)) query = query.Where(log => log.Level == level);

				// WARNING - Change this query if RemoveClassPrefixList is modified
				if (!string.IsNullOrWhiteSpace(classname)) query = query.Where(log => log.LoggerName == classname || log.LoggerName == fullclassnames[0] || log.LoggerName == fullclassnames[1]);

				data = query.AsEnumerable().Select(log => new LogEntry(log)).ToList();
				*/

				var filter = TableQuery.CombineFilters(
					TableQuery.GenerateFilterCondition(nameof(LogEntity.PartitionKey), QueryComparisons.GreaterThan, mindatetick),
					TableOperators.And,
					TableQuery.GenerateFilterCondition(nameof(LogEntity.PartitionKey), QueryComparisons.LessThanOrEqual, maxdatetick)
				);

				if (!string.IsNullOrWhiteSpace(level)) {
					filter = TableQuery.CombineFilters(
						filter,
						TableOperators.And,
						TableQuery.GenerateFilterCondition(nameof(LogEntity.Level), QueryComparisons.Equal, level)
					);
				} else {
					filter = TableQuery.CombineFilters(
						filter,
						TableOperators.And,
						TableQuery.GenerateFilterCondition(nameof(LogEntity.Level), QueryComparisons.NotEqual, "DEBUG")
					);
				}

				if (!string.IsNullOrWhiteSpace(classname)) {
					var cfilter = TableQuery.GenerateFilterCondition(nameof(LogEntity.LoggerName), QueryComparisons.Equal, classname);

					foreach (var fullclassname in fullclassnames) {
						cfilter = TableQuery.CombineFilters(
							cfilter,
							TableOperators.Or,
							TableQuery.GenerateFilterCondition(nameof(LogEntity.LoggerName), QueryComparisons.Equal, fullclassname)
						);
					}

					filter = TableQuery.CombineFilters(filter, TableOperators.And, cfilter);
				}

				var tquery = new TableQuery<LogEntity>().Where(filter);

				var list = await cloud.ExecuteQueryAsync<LogEntity>(tquery).ConfigureAwait(false);

				data = list.AsEnumerable().Select(log => new LogEntry(log)).ToList();
			}

			if (UseLogFiles) {
				var (_, lines) = await ReadLogFileAsync(date, ct).ConfigureAwait(false);

				if (lines == null) {
					data = new LogEntry[0];
				} else {
					var query = lines.Select(line => LogLineRegex.Match(line)).Where(match => match.Success);

					// If line match unsuccessful, then it is an error dump
					if (!string.IsNullOrWhiteSpace(level)) {
						level = level.Trim();
						query = query.Where(match => match.Groups["level"].Value.Equals(level, StringComparison.OrdinalIgnoreCase));
					} else {
						// If level is null, then get all except DEBUG
						query = query.Where(match => !match.Groups["level"].Value.Equals("DEBUG", StringComparison.OrdinalIgnoreCase));
					}

					if (!string.IsNullOrWhiteSpace(classname)) {
						query = query.Where(match =>
											match.Groups["class"].Value == classname ||
											fullclassnames.Contains(match.Groups["class"].Value));
					}

					data = query.Select(match => {
						var classtext = match.Groups["class"].Value.Trim();
						classtext = classtext.Substring(RemoveClassPrefixList.FirstOrDefault(prefix => classtext.StartsWith(prefix))?.Length ?? 0);

						return new LogEntry()
						{
							Time = DateTimeOffset.Parse($"{match.Groups["date"].Value}T{match.Groups["time"].Value.Replace(",", ".")}", null, DateTimeStyles.AssumeLocal),
							Level = match.Groups["level"].Value.Trim(),
							Class = classtext,
							Thread = match.Groups["thread"].Value.Trim(),
							Message = match.Groups["message"].Value
						};
					}).ToList();
				}
			}

			if (data == null) throw new ApplicationException();

			// Download log entries

			switch (format) {
				case DataFileFormats.JSON: return Json(data.OrderByDescending(log => log.Time));
				case DataFileFormats.CSV: return Content(BuildCSVFile(LogEntry.HeaderNames, data), "text/csv", Encoding.UTF8);
				case DataFileFormats.TSV: return Content(BuildCSVFile(LogEntry.HeaderNames, data, "\t", false), "text/csv", Encoding.UTF8);
				case DataFileFormats.XLS:
				case DataFileFormats.XLSX: {
						IWorkbook xls;
						string mime;
						string ext;

						switch (format) {
							case DataFileFormats.XLS: xls = new HSSFWorkbook(); ext = ".xls"; mime = "application/vnd.ms-excel"; break;
							case DataFileFormats.XLSX: xls = new XSSFWorkbook(); ext = ".xlsx"; mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"; break;
							default: throw new ApplicationException();
						}

						using (var stream = new MemoryStream()) {
							BuildXLSFile(stream, xls, "Logs", LogEntry.HeaderNames, data);
							var filedata = stream.ToArray();
							return File(filedata, mime, "logs" + ext);
						}
					}

				default: throw new ApplicationException();
			}
		}

		[HttpGet("{date}/info")]
		[Authorize(Roles = nameof(Filters.All) + ", Org_" + DataStore.DefaultOrgId)]
		public async Task<IActionResult> GetLogFileInfo (string date, CancellationToken ct)
		{
			if (UseAzureStorage) {
				var cloud = ConnectToStorage();
				var startdate = DateTime.Parse(date);
				var enddate = startdate.AddDays(1);

				var maxdatetick = (DateTime.MaxValue.Ticks - startdate.Ticks + 1).ToString("D19");
				var mindatetick = (DateTime.MaxValue.Ticks - enddate.Ticks + 1).ToString("D19");

				/*
				var classes = cloud.CreateQuery<LogEntity>()
													.Where(log => log.PartitionKey.CompareTo(mindatetick) >= 0)
													.Where(log => log.PartitionKey.CompareTo(maxdatetick) <= 0)
													.Select(log => log.LoggerName)
													.AsEnumerable()
													.Distinct()
													.Select(logger => logger.Substring(RemoveClassPrefixList.FirstOrDefault(prefix => logger.StartsWith(prefix))?.Length ?? 0))
													.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
													.ToList();
				*/

				var tquery = new TableQuery<LogEntity>().Where(TableQuery.CombineFilters(
															TableQuery.GenerateFilterCondition(nameof(LogEntity.PartitionKey), QueryComparisons.GreaterThan, mindatetick),
															TableOperators.And,
															TableQuery.GenerateFilterCondition(nameof(LogEntity.PartitionKey), QueryComparisons.LessThanOrEqual, maxdatetick)
														)).Select(new[] { nameof(LogEntity.LoggerName) });

				var lines = await cloud.ExecuteQueryAsync<LogEntity>(tquery).ConfigureAwait(false);

				var classes = lines.Select(x => x.LoggerName)
													.Distinct()
													.Select(logger => logger.Substring(RemoveClassPrefixList.FirstOrDefault(prefix => logger.StartsWith(prefix))?.Length ?? 0))
													.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
													.ToList();

				return Ok(new {
					Lines = lines.Count(),
					Classes = classes
				});
			}

			if (UseLogFiles) {
				var (_, lines) = await ReadLogFileAsync(date, ct).ConfigureAwait(false);
				if (lines == null) return null;

				var classes = lines
												.Select(line => LogLineRegex.Match(line))
												.Where(match => match.Success)
												.GroupBy(match => match.Groups["class"].Value)
												.Select(g => g.Key.Substring(RemoveClassPrefixList.FirstOrDefault(prefix => g.Key.StartsWith(prefix))?.Length ?? 0))
												.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
												.ToList();

				return Ok(new {
					Lines = lines.Count(),
					Classes = classes
				});
			}

			return StatusCode(500);
		}

		[HttpGet("test")]
		[Authorize(Roles = nameof(Filters.All) + ", Org_" + DataStore.DefaultOrgId)]
		public IActionResult Test ()
		{
			return Ok(
				UseLogFiles ? $"[ASP.NET Web API] Time now is {DateTime.Now.ToString("d/M/yyyy h:mm:ss tt")}. Log files path is [{LogsPath}]." :
				UseAzureStorage ? $"[ASP.NET Web API] Time now is {DateTime.Now.ToString("d/M/yyyy h:mm:ss tt")}. Log files is at Azure storage account [{LogsStorageAccount}]." :
				$"[ASP.NET Web API] Time now is {DateTime.Now.ToString("d/M/yyyy h:mm:ss tt")}. No logs specified."
			);
		}

		[HttpGet("testdb")]
		[Authorize(Roles = nameof(Filters.All) + ", Org_" + DataStore.DefaultOrgId)]
		public IActionResult TestDB ()
		{
			var root = LogsPath;

			return Ok(
				Directory.GetFiles(root, "*.log").Select(fn => {
					var match = LogFileRegex.Match(fn);
					if (!match.Success) return new { Path = fn, Date = (DateTime?) null };

					var datestr = match.Groups["date"].Value;
					var date = DateTime.Parse(datestr.Substring(0, 4) + "-" + datestr.Substring(4, 2) + "-" + datestr.Substring(6, 2));
					return new { Path = fn, Date = (DateTime?) date };
				}).OrderByDescending(x => x.Date).ToList()
			);
		}
	}
}