using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace iChen.Web
{
	public partial class StatusController : Microsoft.AspNetCore.Mvc.Controller
	{
		[HttpGet("status")]
		public IActionResult GetStatus ()
		{
			if (!Sessions.IsAuthorized(Request, out var orgId)) return Unauthorized();

			var orgPrefix = orgId + ":";

			var status = new Hosting.Status()
			{
				Started = Hosting.CurrentStatus.Started,
				IsRunning = Hosting.CurrentStatus.IsRunning,
				Version = Hosting.CurrentStatus.Version,
				Port = Hosting.CurrentStatus.Port,
				OpenProtocol = Hosting.CurrentStatus.OpenProtocol
			};

			foreach (var entry in Hosting.CurrentStatus.Controllers.Where(kv => kv.Value.StartsWith(orgPrefix, StringComparison.OrdinalIgnoreCase))) {
				var text = entry.Value.Substring(orgPrefix.Length);
				status.Controllers[entry.Key] = text;
			}
			foreach (var entry in Hosting.CurrentStatus.Clients.Where(kv => kv.Value.StartsWith(orgPrefix, StringComparison.OrdinalIgnoreCase))) {
				var text = entry.Value.Substring(orgPrefix.Length);
				status.Clients[entry.Key] = text;
			}

			return Ok(status);
		}

		[HttpGet("test")]
		public IActionResult Test () => Ok($"[ASP.NET Web API] Time now is {DateTime.Now.ToString("d/M/yyyy h:mm:ss tt")}. User = {Sessions.GetCurrentUser(Request)?.ID ?? 0}.");
	}
}