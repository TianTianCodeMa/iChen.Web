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
		[Route("audit/{controllerId}")]
		public async Task<IActionResult> GetAuditTrailAsync (uint controllerId, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, Filters.Audit)) return Unauthorized();

			return await WebDataDownloadAsync<AuditTrailX>(Storage.AuditTrailTable, "AuditTrail", orgId, controllerId, from, to, Utils.GetSorting(sort), null, format, timezone);
		}

		[HttpGet]
		[Route("audit/{controllerId}/{variable}")]
		public async Task<IActionResult> GetAuditTrailAsync (uint controllerId, string variable, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, Filters.Audit)) return Unauthorized();

			return await WebDataDownloadAsync<AuditTrailX>(Storage.AuditTrailTable, "AuditTrail", orgId, controllerId, from, to, Utils.GetSorting(sort), new KeyEqualsPredicate<AuditTrailX>(variable, AuditTrailX.DatabaseKeyField), format, timezone);
		}
	}
}