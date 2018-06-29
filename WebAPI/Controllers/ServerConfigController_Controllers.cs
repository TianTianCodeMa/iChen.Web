using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace iChen.Web
{
	public partial class ServerConfigController
	{
		private readonly Regex IPorSerialPortRegex = new Regex(
			@"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(\:\d{1,5})|COM\d+|tty\w+)?$",
				RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.Singleline);

		#region Data class

		public class ControllerX : Persistence.Server.Controller
		{
			public ControllerX () : base()
			{
			}

			public ControllerX (Persistence.Server.Controller controller) : base()
			{
				this.ID = controller.ID;
				this.OrgId = controller.OrgId;
				base.IsEnabled = controller.IsEnabled;
				this.Name = controller.Name;
				base.Type = controller.Type;
				this.Version = controller.Version;
				this.Model = controller.Model;
				this.IP = controller.IP;
				this.Created = controller.Created;
				this.Modified = controller.Modified;
			}

			public Persistence.Server.Controller GetBase ()
			{
				return new Persistence.Server.Controller()
				{
					ID = base.ID,
					OrgId = base.OrgId,
					IsEnabled = base.IsEnabled,
					Name = base.Name,
					Type = base.Type,
					Version = base.Version,
					Model = base.Model,
					IP = base.IP,
					Created = base.Created,
					Modified = base.Modified
				};
			}

			[JsonIgnore]
			public bool? m_IsEnabled = null;

			[JsonProperty("isEnabled", DefaultValueHandling = DefaultValueHandling.Include, Order = 1)]
			public new bool? IsEnabled
			{
				get { return base.IsEnabled; }
				set {
					m_IsEnabled = value;
					if (value.HasValue) base.IsEnabled = value.Value;
				}
			}

			[JsonIgnore]
			public OpenProtocol.ControllerTypes BaseControllerType
			{
				get { return base.Type; }
				set { base.Type = value; }
			}

			[JsonIgnore]
			public string m_ControllerTypeText = null;

			[JsonProperty("type", Order = 99)]
			public new string Type
			{
				get { return BaseControllerType.ToString(); }
				set { m_ControllerTypeText = value; }
			}
		}

		#endregion Data class

		// http://url/config/controllers
		[HttpGet("controllers")]
		public async Task<IActionResult> GetControllersAsync (CancellationToken ct)
		{
			if (!Sessions.IsAuthorized(Request, out var orgId, OpenProtocol.Filters.None)) return Unauthorized();

			return Ok((await db.Controllers.AsNoTracking()
											.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
											.ToListAsync(ct)
											.ConfigureAwait(false)
											).Select(c => new ControllerX(c))
											.ToDictionary(c => c.ID));
		}

		// http://url/config/controllers/{id}
		[HttpGet("controllers/{id}")]
		public async Task<IActionResult> GetControllerAsync (int id, CancellationToken ct)
		{
			if (id <= 0) return NotFound();

			if (!Sessions.IsAuthorized(Request, out var orgId, OpenProtocol.Filters.None)) return Unauthorized();

			var controller = await db.Controllers.AsNoTracking()
																.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
																.SingleOrDefaultAsync(c => c.ID == id, ct)
																.ConfigureAwait(false);

			if (controller == null) return NotFound();

			return Ok(new ControllerX(controller));
		}

		// http://url/config/controllers/{id}/molds
		[HttpGet("controllers/{id}/molds")]
		public async Task<IActionResult> GetControllerMoldsAsync (int id, CancellationToken ct)
		{
			if (id <= 0) return NotFound();

			if (!Sessions.IsAuthorized(Request, out var orgId, OpenProtocol.Filters.Mold)) return Unauthorized();

			var ctrl = await db.Controllers.AsNoTracking().Include(c => c.Molds)
													.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
													.SingleOrDefaultAsync(c => c.ID == id, ct)
													.ConfigureAwait(false);

			if (ctrl == null) return NotFound();

			return Ok(ctrl.Molds.Select(x => new MoldX(x)).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase));
		}

		// http://url/config/controllers/{id}/molds/{moldId}
		[HttpGet("controllers/{id}/molds/{moldId}")]
		public async Task<IActionResult> GetControllerMoldAsync (int id, string moldId, CancellationToken ct)
		{
			if (id <= 0) return NotFound();
			if (string.IsNullOrWhiteSpace(moldId)) return NotFound();

			if (!Sessions.IsAuthorized(Request, out var orgId)) return Unauthorized();

			var ctrl = await db.Controllers.AsNoTracking().Include(c => c.Molds)
													.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
													.SingleOrDefaultAsync(c => c.ID == id, ct)
													.ConfigureAwait(false);

			if (ctrl == null) return NotFound();

			var mx = await db.Molds.AsNoTracking().Include(m => m.MoldSettings)
												.Where(m => m.ControllerId == id)
												.SingleOrDefaultAsync(x => x.Name.Equals(moldId, StringComparison.OrdinalIgnoreCase), ct)
												.ConfigureAwait(false);

			if (mx == null) return NotFound();

			return Ok(new MoldX(mx));
		}

		// http://url/config/controllers - Add
		[HttpPost("controllers")]
		public async Task<IActionResult> AddControllerAsync ([FromBody] ControllerX controller, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(controller.Name)) return BadRequest($"Invalid controller name: [{controller.Name}].");
			controller.Name = controller.Name.Trim();

			OpenProtocol.ControllerTypes ctype = OpenProtocol.ControllerTypes.Unknown;

			if (!string.IsNullOrWhiteSpace(controller.m_ControllerTypeText)) {
				if (!Enum.TryParse(controller.m_ControllerTypeText.Trim(), true, out ctype)) return BadRequest($"Invalid controller type: [{controller.m_ControllerTypeText}].");
			}

			controller.BaseControllerType = ctype;

			controller.Version = string.IsNullOrWhiteSpace(controller.Version) ? "0.0.0" : controller.Version.Trim();

			controller.Model = string.IsNullOrWhiteSpace(controller.Model) ? "Unknown" : controller.Model.Trim();

			controller.IP = string.IsNullOrWhiteSpace(controller.IP) ? "1.1.1.1" : controller.IP.Trim();

			if (!IPorSerialPortRegex.IsMatch(controller.IP)) return BadRequest($"Invalid IP address: [{controller.IP}].");

			if (!Sessions.IsAuthorized(Request, out var orgId)) return Unauthorized();

			controller.OrgId = orgId;
			controller.Modified = null;

			var cx = await db.Controllers
												.SingleOrDefaultAsync(c => c.ID == controller.ID, ct)
												.ConfigureAwait(false);
			if (cx != null) {
				if (cx.OrgId.Equals(controller.OrgId, StringComparison.OrdinalIgnoreCase)) return BadRequest($"Controller ID [{controller.ID}] already exists.");
				return BadRequest($"Invalid controller ID [{controller.ID}].");
			}

			cx = controller.GetBase();
			db.Controllers.Add(cx);
			await db.SaveChangesAsync(ct).ConfigureAwait(false);

			return Created($"controllers/{cx.ID}", new ControllerX(cx));
		}

		// http://url/config/controllers/{id} - Update
		[HttpPost("controllers/{id}")]
		public async Task<IActionResult> UpdateControllerAsync (int id, [FromBody] ControllerX delta, CancellationToken ct)
		{
			if (id <= 0) return NotFound();
			if (delta.ID != 0) return BadRequest($"Controller ID cannot be changed.");

			if (delta.Name != null) {
				if (string.IsNullOrWhiteSpace(delta.Name)) return BadRequest($"Invalid controller name: [{delta.Name}].");
				delta.Name = delta.Name.Trim();
			}

			if (!string.IsNullOrEmpty(delta.m_ControllerTypeText)) {
				if (!Enum.TryParse(delta.m_ControllerTypeText.Trim(), true, out OpenProtocol.ControllerTypes type)) return BadRequest($"Invalid controller type: [{delta.m_ControllerTypeText}].");
				delta.BaseControllerType = type;
			}

			if (delta.Version != null) {
				if (string.IsNullOrWhiteSpace(delta.Version)) return BadRequest($"Invalid version: [{delta.Version}].");
				delta.Version = delta.Version.Trim();
			}

			if (delta.Model != null) {
				if (string.IsNullOrWhiteSpace(delta.Model)) return BadRequest($"Invalid machine model: [{delta.Model}].");
				delta.Model = delta.Model.Trim();
			}

			if (delta.IP != null) {
				if (string.IsNullOrWhiteSpace(delta.IP)) return BadRequest($"Invalid IP address: [{delta.IP}].");
				delta.IP = delta.IP.Trim();

				if (!IPorSerialPortRegex.IsMatch(delta.IP)) return BadRequest($"Invalid IP address: [{delta.IP}].");
			}

			if (!Sessions.IsAuthorized(Request, out var orgId)) return Unauthorized();

			var controller = await db.Controllers
																.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
																.SingleOrDefaultAsync(c => c.ID == id, ct)
																.ConfigureAwait(false);

			if (controller == null) return NotFound();

			if (delta.m_IsEnabled.HasValue) controller.IsEnabled = delta.IsEnabled.Value;
			if (delta.Name != null) controller.Name = delta.Name;
			if (!string.IsNullOrWhiteSpace(delta.m_ControllerTypeText)) controller.Type = delta.BaseControllerType;
			if (delta.Version != null) controller.Version = delta.Version;
			if (delta.Model != null) controller.Model = delta.Model;
			if (delta.IP != null) controller.IP = delta.IP;

			delta.Modified = DateTime.Now;

			await db.SaveChangesAsync(ct).ConfigureAwait(false);

			return Ok(new ControllerX(controller));
		}

		// http://url/config/controllers/{id}
		[HttpDelete("controllers/{id}")]
		public async Task<IActionResult> DeleteControllerAsync (int id, CancellationToken ct)
		{
			if (id <= 0) return NotFound();

			if (!Sessions.IsAuthorized(Request, out var orgId)) return Unauthorized();

			var controller = await db.Controllers
																.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
																.SingleOrDefaultAsync(c => c.ID == id, ct)
																.ConfigureAwait(false);

			if (controller == null) return NotFound();

			db.Controllers.Remove(controller);
			await db.SaveChangesAsync(ct).ConfigureAwait(false);

			return Ok(new ControllerX(controller));
		}
	}
}