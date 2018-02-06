using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using iChen.Analytics;
using iChen.OpenProtocol;
using iChen.Persistence.Cloud;
using Microsoft.AspNetCore.Mvc;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace iChen.Web.Analytics
{
	public partial class ReportController
	{
		[HttpGet]
		[Route("cycledata/{controllerId}")]
		public async Task<IActionResult> GetCycleDataAsync (uint controllerId, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, Filters.Cycle)) return Unauthorized();

			return await WebDataDownloadAsync<CycleDataX>(Storage.CycleDataTable, "Cycle Data", orgId, controllerId, from, to, Utils.GetSorting(sort), null, format, timezone);
		}

		[HttpGet]
		[Route("cycledata/{controllerId}/{variable}")]
		public async Task<IActionResult> GetCycleDataVariableAsync (uint controllerId, string variable, DateTimeOffset? from = null, DateTimeOffset? to = null, DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, Filters.Cycle)) return Unauthorized();

			if (string.IsNullOrWhiteSpace(variable)) return BadRequest($"Invalid variable name: {variable}");

			IEnumerable<CycleDataX> result;

			try {
				Utils.ProcessDateTimeRange(ref from, ref to);

				using (var ana = new AnalyticsEngine(db)) {
					result = await ana.GetDataAsync<CycleDataX>(Storage.CycleDataTable, from.Value, to.Value, null, Sorting.ByTime, orgId, controllerId, variable);
					if (result == null) return NotFound();
				}
			} catch (ArgumentException ex) {
				return BadRequest(ex.Message);
			}

			var data = result.AsParallel().Select(x => new TimeValue<double>(x.Time, x.ContainsKey(variable) ? x[variable] : 0.0)).ToList();

			// Encode result

			switch (format) {
				case DataFileFormats.JSON: return Json(data);
				case DataFileFormats.CSV: return Content(DataFileGenerator.BuildCSVFile(TimeValue<double>.Headers, data, timezone), "text/csv", Encoding.UTF8);
				case DataFileFormats.TSV: return Content(DataFileGenerator.BuildCSVFile(TimeValue<double>.Headers, data, timezone, "\t", false), "text/csv", Encoding.UTF8);
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
							DataFileGenerator.BuildXLSFile(stream, xls, variable, TimeValue<double>.Headers, data, timezone);
							var filedata = stream.ToArray();
							return File(filedata, mime, variable + ext);
						}
					}

				default: throw new ApplicationException();
			}
		}
	}
}