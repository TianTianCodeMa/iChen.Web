using System;
using System.Linq;
using iChen.OpenProtocol;
using Microsoft.AspNetCore.Mvc;

namespace iChen.Web
{
	public partial class SessionController : Microsoft.AspNetCore.Mvc.Controller
	{
		public class LoginInfo
		{
			public string Name { get; set; }
			public string Password { get; set; }
		}

		[HttpPost("login")]
		public IActionResult Login ([FromBody] LoginInfo info)
		{
			if (info == null || string.IsNullOrWhiteSpace(info.Name) || string.IsNullOrWhiteSpace(info.Password))
				return BadRequest($"Missing user name or password for login.");

			return Login(info.Name, info.Password);
		}

		[HttpGet("login")]
		public IActionResult Login (string user, string password)
		{
			var guid = Sessions.Login(user, password);
			if (string.IsNullOrWhiteSpace(guid)) return Unauthorized();

			var ux = Sessions.GetSession(guid);

			// Return the user
			Response.Cookies.Append(Sessions.SessionCookieID, ux.SessionID);

			return Created("user", ux);
		}

		[HttpGet("logout")]
		public IActionResult LogOut ()
		{
			var ux = Sessions.GetCurrentUser(Request);
			if (ux == null) return NoContent();

			Sessions.Logout(Sessions.GetCurrentUser(Request)?.SessionID);

			return Ok(ux);
		}

		[HttpGet("sessions")]
		public IActionResult GetAllUserSessions ()
		{
			if (!Sessions.IsAuthorized(Request, out var orgId)) return Unauthorized();

			var sessions = Sessions.GetAllSessions().Where(ss => ss.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase)).ToList();

			return Ok(sessions);
		}

		[HttpGet("sessions/{token}")]
		public IActionResult GetUserSession (string token)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId)) return Unauthorized();

			var session = Sessions.GetSession(token);
			if (session == null || session.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase)) return NotFound();

			return Ok(session);
		}

		[HttpGet("user")]
		public IActionResult GetUserInfo ()
		{
			if (!Sessions.IsAuthorized(Request, out _, Filters.None)) return Unauthorized();

			return Ok(Sessions.GetCurrentUser(Request));
		}
	}
}