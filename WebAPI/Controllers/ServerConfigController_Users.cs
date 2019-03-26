using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using iChen.OpenProtocol;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace iChen.Web
{
	public partial class ServerConfigController
	{
		#region Data class

		public class UserX : User
		{
			public UserX () : base()
			{
			}

			public UserX (User user) : base()
			{
				this.ID = user.ID;
				this.OrgId = user.OrgId;
				this.Password = user.Password;
				this.Name = user.Name;
				base.IsEnabled = user.IsEnabled;
				base.Filters = user.Filters;
				base.AccessLevel = user.AccessLevel;
				this.Created = user.Created;
				this.Modified = user.Modified;
			}

			public User GetBase ()
			{
				return new User()
				{
					ID = base.ID,
					OrgId = base.OrgId,
					Password = base.Password,
					Name = base.Name,
					IsEnabled = base.IsEnabled,
					Filters = base.Filters,
					AccessLevel = base.AccessLevel,
					Created = base.Created,
					Modified = base.Modified
				};
			}

			[JsonIgnore]
			public bool? m_IsEnabled = null;

			[JsonProperty("isEnabled", DefaultValueHandling = DefaultValueHandling.Include, Order = 97)]
			public new bool? IsEnabled
			{
				get { return base.IsEnabled; }
				set {
					m_IsEnabled = value;
					if (value.HasValue) base.IsEnabled = value.Value;
				}
			}

			[JsonIgnore]
			public byte? m_AccessLevel = null;

			[JsonProperty("accessLevel", DefaultValueHandling = DefaultValueHandling.Include, Order = 98)]
			public new byte? AccessLevel
			{
				get { return base.AccessLevel; }
				set {
					m_AccessLevel = value;
					if (value.HasValue) base.AccessLevel = value.Value;
				}
			}

			[JsonIgnore]
			public Filters BaseFilters
			{
				get { return base.Filters; }
				set { base.Filters = value; }
			}

			[JsonIgnore]
			public string m_FiltersText = null;

			[JsonProperty("filters", Order = 99)]
			public new string Filters
			{
				get { return BaseFilters.ToString(); }
				set { m_FiltersText = value; }
			}
		}

		#endregion Data class

		// http://url/config/users
		[HttpGet("users")]
		[Authorize(Roles = nameof(Filters.All))]
		public async Task<IActionResult> GetUsersAsync (CancellationToken ct)
		{
			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var ux = (await db.Users.AsNoTracking()
												.Where(user => user.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
												.ToListAsync(ct)
												.ConfigureAwait(false)
												).Select(user => new UserX(user))
												.ToList();

				return Ok(ux);
			}
		}

		// http://url/config/users/{id}
		[HttpGet("users/{id}")]
		[Authorize(Roles = nameof(Filters.All))]
		public async Task<IActionResult> GetUserAsync (int id, CancellationToken ct)
		{
			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var user = await db.Users.AsNoTracking()
													.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
													.SingleOrDefaultAsync(x => x.ID == id, ct)
													.ConfigureAwait(false);

				if (user == null) return NotFound();

				return Ok(new UserX(user));
			}
		}

		// http://url/config/users - Add
		[HttpPost("users")]
		[Authorize(Roles = nameof(Filters.All))]
		public async Task<IActionResult> AddUserAsync ([FromBody] UserX user, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(user.Password)) return BadRequest("Missing password.");
			user.Password = user.Password.Trim();

			if (string.IsNullOrWhiteSpace(user.Name)) return BadRequest("Missing user name.");
			user.Name = user.Name.Trim();

			if (string.IsNullOrWhiteSpace(user.m_FiltersText)) return BadRequest($"Missing filters.");
			if (!Enum.TryParse<Filters>(user.m_FiltersText.Trim(), true, out var filter)) return BadRequest($"Invalid filters: [{user.m_FiltersText}].");
			user.BaseFilters = filter;

			if (user.AccessLevel.Value > 10) return BadRequest($"Invalid access level (0-10): [{user.AccessLevel}].");

			var orgId = HttpContext.GetOrg();

			user.Modified = null;
			user.OrgId = orgId;

			using (var db = new ConfigDB()) {
				var ux = await db.Users
												.Where(x => x.OrgId.Equals(user.OrgId, StringComparison.OrdinalIgnoreCase))
												.SingleOrDefaultAsync(x => x.Password.Equals(user.Password, StringComparison.OrdinalIgnoreCase), ct)
												.ConfigureAwait(false);

				if (ux != null) return BadRequest($"Password [{user.Password}] already exists.");

				ux = await db.Users
											.Where(x => x.OrgId.Equals(user.OrgId, StringComparison.OrdinalIgnoreCase))
											.SingleOrDefaultAsync(x => x.Name.Equals(user.Name, StringComparison.OrdinalIgnoreCase), ct)
											.ConfigureAwait(false);

				if (ux != null) return BadRequest($"User name [{user.Name}] already exists.");

				ux = user.GetBase();
				db.Users.Add(ux);
				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Created($"users/{ux.ID}", new UserX(ux));
			}
		}

		// http://url/config/users/{id} - Update
		[HttpPost("users/{id}")]
		[Authorize(Roles = nameof(Filters.All))]
		public async Task<IActionResult> UpdateUserAsync (int id, [FromBody] UserX delta, CancellationToken ct)
		{
			if (delta.Password != null) {
				if (string.IsNullOrWhiteSpace(delta.Password)) return BadRequest("Missing password.");
				delta.Password = delta.Password.Trim();
			}

			if (delta.Name != null) {
				if (string.IsNullOrWhiteSpace(delta.Name)) return BadRequest("Missing user name.");
				delta.Name = delta.Name.Trim();
			}

			if (delta.m_AccessLevel.HasValue) {
				if (delta.AccessLevel.Value > 10) return BadRequest($"Invalid access level (0-10): [{delta.AccessLevel}].");
			}

			if (!string.IsNullOrEmpty(delta.m_FiltersText)) {
				if (!Enum.TryParse<Filters>(delta.m_FiltersText.Trim(), true, out var filter)) return BadRequest($"Invalid filters: [{delta.m_FiltersText}].");
				delta.BaseFilters = filter;
			}

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var user = await db.Users
													.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
													.SingleOrDefaultAsync(x => x.ID == id, ct)
													.ConfigureAwait(false);

				if (user == null) return NotFound();

				if (delta.m_IsEnabled.HasValue) user.IsEnabled = delta.IsEnabled.Value;

				if (delta.Name != null) {
					var cx = await db.Users
														.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.SingleOrDefaultAsync(x => x.Name.Equals(delta.Name, StringComparison.OrdinalIgnoreCase), ct)
														.ConfigureAwait(false);

					if (cx != null) return BadRequest($"User name [{delta.Name}] already exists.");

					user.Name = delta.Name;
				}

				if (delta.Password != null) user.Password = delta.Password;
				if (!string.IsNullOrWhiteSpace(delta.m_FiltersText)) user.Filters = delta.BaseFilters;
				if (delta.m_AccessLevel.HasValue) user.AccessLevel = delta.AccessLevel.Value;

				delta.Modified = DateTime.Now;

				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Ok(new UserX(user));
			}
		}

		// http://url/config/users/{id}
		[HttpDelete("users/{id}")]
		[Authorize(Roles = nameof(Filters.All))]
		public async Task<IActionResult> DeleteUserAsync (int id, CancellationToken ct)
		{
			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var user = await db.Users
													.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
													.SingleOrDefaultAsync(x => x.ID == id, ct)
													.ConfigureAwait(false);

				if (user == null) NotFound();

				db.Users.Remove(user);
				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Ok(new UserX(user));
			}
		}
	}
}