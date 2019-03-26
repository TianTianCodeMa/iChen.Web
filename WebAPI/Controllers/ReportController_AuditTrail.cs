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
		[Route("audit/{controllerId}")]
		[Authorize(Roles = nameof(Filters.Audit))]
		public async Task<IActionResult> GetAuditTrailAsync (uint controllerId, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
			=> await WebDataDownloadAsync<AuditTrailX>(Storage.AuditTrailTable, "AuditTrail", HttpContext.GetOrg(), controllerId, from, to, Utils.GetSorting(sort), null, format, timezone);

		[HttpGet]
		[Route("audit/{controllerId}/{variable}")]
		[Authorize(Roles = nameof(Filters.Audit))]
		public async Task<IActionResult> GetAuditTrailAsync (uint controllerId, string variable, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
			=> await WebDataDownloadAsync<AuditTrailX>(Storage.AuditTrailTable, "AuditTrail", HttpContext.GetOrg(), controllerId, from, to, Utils.GetSorting(sort), new KeyEqualsPredicate<AuditTrailX>(variable, AuditTrailX.DatabaseKeyField), format, timezone);
	}
}