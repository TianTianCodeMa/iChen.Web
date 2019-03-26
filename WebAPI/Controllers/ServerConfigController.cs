using System;
using System.Linq;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iChen.Web
{
	[Route(WebSettings.Route_Config)]
	public partial class ServerConfigController : Microsoft.AspNetCore.Mvc.Controller
	{
		[HttpGet("test")]
		[AllowAnonymous]
		public IActionResult Test ()
			=> Ok($"[ASP.NET Core Web API] Time now is {DateTime.Now.ToString("d/M/yyyy h:mm:ss tt")}. User = {HttpContext.User?.Identity.Name ?? "None"}.");

		[HttpGet("testdb")]
		public IActionResult TestDB ()
		{
			try {
				using (var db = new ConfigDB()) {
					var nc = db.Controllers.Count();
					var nu = db.Users.Count();

					return Ok($"{nc} controllers, {nu} users.");
				}
			} catch (Exception ex) {
				return Ok(ex.ToString());
			}
		}
	}
}