using System;
using System.Linq;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Mvc;

namespace iChen.Web
{
	[Route(WebSettings.Route_Config)]
	public partial class ServerConfigController : Microsoft.AspNetCore.Mvc.Controller
	{
		private readonly ConfigDB db;

		public ServerConfigController (ConfigDB context) : base() { this.db = context; }

		[HttpGet("test")]
		public IActionResult Test ()
			=> Ok($"[ASP.NET Core Web API] Time now is {DateTime.Now.ToString("d/M/yyyy h:mm:ss tt")}. User = {Sessions.GetCurrentUser(Request)?.ID ?? 0}.");

		[HttpGet("testdb")]
		public IActionResult TestDB ()
		{
			if (!Sessions.IsAuthorized(Request, out _)) return Unauthorized();

			try {
				var nc = db.Controllers.Count();
				var nu = db.Users.Count();

				return Ok($"{nc} controllers, {nu} users.");
			} catch (Exception ex) {
				return Ok(ex.ToString());
			}
		}
	}
}