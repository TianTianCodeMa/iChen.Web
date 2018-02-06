using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using iChen.Analytics;
using iChen.OpenProtocol;
using iChen.Persistence.Cloud;
using Microsoft.AspNetCore.Mvc;

namespace iChen.Web.Analytics
{
	public partial class ReportController
	{
		public const string NoValueMarker = "NoValue";

		[HttpGet]
		[Route("events/{controllerId:min(1)}")]
		public async Task<IActionResult> GetEventsAsync (uint controllerId, DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, Filters.Status)) return Unauthorized();

			return await WebDataDownloadAsync<EventX>(Storage.EventsTable, "Events", orgId, controllerId, from, to, Utils.GetSorting(sort), null, format, timezone);
		}

		[HttpGet]
		[Route("events")]
		public async Task<IActionResult> GetAllEventsAsync (DateTimeOffset? from = null, DateTimeOffset? to = null, string sort = "time", DataFileFormats format = DataFileFormats.JSON, double timezone = 0.0)
			=> await GetEventsAsync(0, from, to, sort, format, timezone);

		[HttpGet]
		[Route("events/{field:regex(^(IP|JobMode|OpMode|Connected|JobCard|Operator|Mold)$)}")]
		public async Task<IActionResult> GetEventsAggregatedAsync (string field, uint controllerId = 0, TimeSpan? step = null, DateTimeOffset? from = null, DateTimeOffset? to = null, double? timezone = null)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, Filters.Status)) return Unauthorized();

			if (step.HasValue && step.Value.Ticks < 0) {
				if (step.Value.TotalDays == -7) {
					// Weekly
				} else {
					switch ((int) step.Value.TotalDays) {
						case -30: // Monthly
						case -90: // Quarterly
						case -180: // Half-yearly
							break;

						default: return BadRequest($"Invalid step: {step}");
					}
				}
			}

			Func<EventX, object> selfunc = null;

			switch (field) {
				case "IP": selfunc = ev => ev.IP; break;
				case "JobMode": selfunc = ev => ev.JobMode; break;
				case "OpMode": selfunc = ev => ev.OpMode; break;
				case "Connected": selfunc = ev => ev.Connected; break;
				case "JobCard": selfunc = ev => ev.JobCardId == "" ? null : ev.JobCardId; break;
				case "Operator": selfunc = ev => ev.OperatorId; break;
				case "Mold": selfunc = ev => ev.MoldId == Guid.Empty ? null : ev.MoldId; break;

				default: return BadRequest($"Invalid aggregate grouping: {field}");
			}

			if (!timezone.HasValue) {
				if (from.HasValue) timezone = from.Value.Offset.TotalMinutes;
				else if (to.HasValue) timezone = to.Value.Offset.TotalMinutes;
				else timezone = DateTimeOffset.Now.Offset.TotalMinutes;
			}

			if (!AnalyticsEngine.IsInitialized) return NotFound();

			try {
				Utils.ProcessDateTimeRange(ref from, ref to);

				// Get the data

				using (var ana = new AnalyticsEngine(db)) {
					var result = await ana.GetEventsTimeSplit(from.Value, to.Value, selfunc, field, NoValueMarker, true, step ?? default(TimeSpan), TimeSpan.FromMinutes(timezone.Value), orgId, new[] { controllerId }).ConfigureAwait(false);
					if (result == null) return NotFound();

					return Ok(result);
				}
			} catch (ArgumentException ex) {
				return BadRequest(ex.Message);
			}
		}

		[HttpGet]
		[Route("events/{controllerId:min(0)}/{field:regex(^(IP|JobMode|OpMode|Connected|JobCard|Operator|Mold)$)}")]
		public async Task<IActionResult> GetAllEventsAggregatedAsync (uint controllerId, string field, TimeSpan? step = null, DateTimeOffset? from = null, DateTimeOffset? to = null, double? timezone = null)
		{
			if (!Sessions.IsAuthorized(Request, out _, Filters.Status)) return Unauthorized();

			IActionResult r;

			try {
				r = await GetEventsAggregatedAsync(field, controllerId, step, from, to, timezone).ConfigureAwait(false);
			} catch (ArgumentException ex) {
				return BadRequest(ex.Message);
			}

			if (!(r is OkObjectResult result)) return r;

			var data = (IReadOnlyDictionary<uint, IList<AnalyticsEngine.TimeSplit<object>>>) result.Value;

			if (controllerId <= 0) {
				return Ok(AnalyticsEngine.CalcEventsTimeSplitAggregate(data));
			} else {
				if (!data.ContainsKey(controllerId)) return NotFound();
				return Ok(data[controllerId]);
			}
		}
	}
}