using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using iChen.OpenProtocol;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iChen.Web
{
	public partial class SessionController : Microsoft.AspNetCore.Mvc.Controller
	{
		private static readonly char[] UserNameSeparators = new[] { '\\' };
		public static SessionStore SessionsCache;

		public class LoginInfo
		{
			public string Name { get; set; }
			public string Password { get; set; }
		}

		public class LoggedInUser
		{
			public int ID { get; set; }
			public string OrgID { get; set; }
			public string Name { get; set; }
			public bool IsEnabled { get; set; }
			public string Filters { get; set; }
			public string[] Roles { get; set; }

			public LoggedInUser () { }

			public LoggedInUser (ClaimsPrincipal user)
			{
				if (user == null) throw new ArgumentNullException(nameof(user));

				// Note: User ID is kept in SerialNumber
				this.ID = int.Parse(user.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.SerialNumber).Value);
				// Note: User Org ID is kept in UserData
				this.OrgID = user.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.UserData).Value;
				this.Name = user.Identity.Name;
				this.IsEnabled = true;

				this.Roles = Enum.GetNames(typeof(Filters))
													.Where(key => key != nameof(OpenProtocol.Filters.None))
													.Where(key => user.IsInRole(key))
													.ToArray();

				if (this.Roles.Length <= 0) this.Roles = null;
				else this.Filters = string.Join(",", this.Roles);
			}
		}

		[HttpPost("login")]
		[AllowAnonymous]
		public async Task<IActionResult> LoginAsync ([FromBody] LoginInfo info)
			=> await LoginAsync(info?.Name, info?.Password);

		[HttpGet("login")]
		[AllowAnonymous]
		public async Task<IActionResult> LoginAsync (string user, string password)
		{
			if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
				return BadRequest($"Missing user name or password for login.");

			user = user.Trim();
			password = password.Trim();

			// Get org ID if present: username can be "orgId\user"
			var orgId = DataStore.DefaultOrgId;
			var flds = user.Split(UserNameSeparators, 2);

			if (flds.Length > 1 && !string.IsNullOrWhiteSpace(flds[0]) && !string.IsNullOrWhiteSpace(flds[1])) {
				orgId = flds[0].Trim();
				user = flds[1].Trim();
			}

			User dbuser;

			using (var db = new ConfigDB()) {
				dbuser = db.Users.AsNoTracking()
										.Where(ux => ux.IsEnabled)
										.Where(ux => ux.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
										.SingleOrDefault(ux => ux.Name.Equals(user, StringComparison.OrdinalIgnoreCase) && ux.Password.Equals(password, StringComparison.OrdinalIgnoreCase));

				if (dbuser == null) return Unauthorized();
			}

			// Create Claims
			var claims = new List<Claim> {
				new Claim(ClaimTypes.Name, dbuser.Name),
				new Claim(ClaimTypes.SerialNumber, dbuser.ID.ToString()),
				new Claim(ClaimTypes.UserData, dbuser.OrgId),
				new Claim(ClaimTypes.Role, "Org_" + dbuser.OrgId)
			};

			foreach (var key in Enum.GetNames(typeof(Filters))) {
				var filter = (Filters) Enum.Parse(typeof(Filters), key);
				if (dbuser.Filters.HasFlag(filter)) claims.Add(new Claim(ClaimTypes.Role, key));
			}

			var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

			var options = new AuthenticationProperties();

			await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, options).ConfigureAwait(false);

			return Created("user", new LoggedInUser(principal));
		}

		[HttpGet("logout")]
		public async Task<IActionResult> LogOutAsync ()
		{
			await HttpContext.SignOutAsync().ConfigureAwait(false);
			return NoContent();
		}

		[HttpGet("user")]
		public IActionResult GetUserInfo ()
		{
			if (HttpContext.User == null) return Unauthorized();
			return Ok(new LoggedInUser(HttpContext.User));
		}
	}
}