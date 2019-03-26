using System;
using System.Threading.Tasks;
using iChen.Analytics;
using iChen.OpenProtocol;
using iChen.Persistence.Cloud;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iChen.Web.Analytics
{
	public partial class ReportController
	{
		[HttpGet]
		[Route("alarms/{controllerId}")]
		[Authorize(Roles = nameof(Filters.Alarms))]
		public async Task<IActionResult> GetAlarmsAsync (uint controllerId, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
			=> await WebDataDownloadAsync<AlarmX>(Storage.AlarmsTable, "Alarms", HttpContext.GetOrg(), controllerId, from, to, Utils.GetSorting(sort), null, format, timezone);

		[HttpGet]
		[Route("alarms/{controllerId}/{alarm}")]
		[Authorize(Roles = nameof(Filters.Alarms))]
		public async Task<IActionResult> GetAlarmsAsync (uint controllerId, string alarm, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
			=> await WebDataDownloadAsync<AlarmX>(Storage.AlarmsTable, "Alarms", HttpContext.GetOrg(), controllerId, from, to, Utils.GetSorting(sort), new KeyEqualsPredicate<AlarmX>(alarm, AlarmX.DatabaseKeyField), format, timezone);
	}
}