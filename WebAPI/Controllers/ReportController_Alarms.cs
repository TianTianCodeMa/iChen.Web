using System;
using System.Threading.Tasks;
using iChen.Analytics;
using iChen.OpenProtocol;
using iChen.Persistence.Cloud;
using Microsoft.AspNetCore.Mvc;

namespace iChen.Web.Analytics
{
	public partial class ReportController
	{
		[HttpGet]
		[Route("alarms/{controllerId}")]
		public async Task<IActionResult> GetAlarmsAsync (uint controllerId, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, Filters.Alarms)) return Unauthorized();

			return await WebDataDownloadAsync<AlarmX>(Storage.AlarmsTable, "Alarms", orgId, controllerId, from, to, Utils.GetSorting(sort), null, format, timezone);
		}

		[HttpGet]
		[Route("alarms/{controllerId}/{alarm}")]
		public async Task<IActionResult> GetAlarmsAsync (uint controllerId, string alarm, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, Filters.Alarms)) return Unauthorized();

			return await WebDataDownloadAsync<AlarmX>(Storage.AlarmsTable, "Alarms", orgId, controllerId, from, to, Utils.GetSorting(sort), new KeyEqualsPredicate<AlarmX>(alarm, AlarmX.DatabaseKeyField), format, timezone);
		}
	}
}