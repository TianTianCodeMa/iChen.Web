using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using iChen.Analytics;
using iChen.Persistence.Cloud;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Mvc;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace iChen.Web.Analytics
{
	[Route(WebSettings.Route_Reports)]
	public partial class ReportController : Microsoft.AspNetCore.Mvc.Controller
	{
		private readonly ConfigDB db;

		public ReportController (ConfigDB context) : base() { this.db = context; }

		public async Task<IActionResult> WebDataDownloadAsync<T> (string table, string title, string orgId, uint controllerId, DateTimeOffset? from = null, DateTimeOffset? to = null, Sorting sort = Sorting.ByTime, IPredicate<T> filter = null, DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
			where T : EntryBase, IDataFileFormatConverter
		{
			IEnumerable<T> data;

			try {
				Utils.ProcessDateTimeRange(ref from, ref to);

				using (var ana = new AnalyticsEngine(db)) {
					data = await ana.GetDataAsync<T>(table, from.Value, to.Value, filter, sort, orgId, controllerId);
					if (data == null) return NotFound();
				}
			} catch (ArgumentException ex) {
				return BadRequest(ex.ToString());
			}

			// Encode result
			string[] headers = null;
			string filename = "data";

			if (format != DataFileFormats.JSON) {
				if (typeof(T) == typeof(EventX)) {
					headers = EventX.Headers; filename = "events";
				} else if (typeof(T) == typeof(AlarmX)) {
					headers = AlarmX.Headers; filename = "alarms";
				} else if (typeof(T) == typeof(AuditTrailX)) {
					headers = AuditTrailX.Headers; filename = "audit";
				} else if (typeof(T) == typeof(CycleDataX)) {
					headers = CycleDataX.Headers.Concat((data as IEnumerable<CycleDataX>).SelectMany(x => x.Data.Keys).Distinct()).ToArray();
					filename = "cycledata";
				} else throw new ApplicationException();
			}

			switch (format) {
				case DataFileFormats.JSON: return Json(data);
				case DataFileFormats.CSV: return Content(DataFileGenerator.BuildCSVFile(headers, data, timezone), "text/csv", Encoding.UTF8);
				case DataFileFormats.TSV: return Content(DataFileGenerator.BuildCSVFile(headers, data, timezone, "\t", false), "text/csv", Encoding.UTF8);
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
							DataFileGenerator.BuildXLSFile(stream, xls, title, headers, data, timezone);
							var filedata = stream.ToArray();

							return File(filedata, mime, filename + ext);
						}
					}

				default: throw new ApplicationException();
			}
		}

		[HttpGet("test")]
		public string Test () => $"[ASP.NET Web API] Time now is {DateTime.Now.ToString("d/M/yyyy h:mm:ss tt")}. The analytics engine is {(AnalyticsEngine.IsInitialized ? null : "not ")}initialized.";
	}
}