using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using iChen.OpenProtocol;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace iChen.Web
{
	public partial class ServerConfigController
	{
		// http://url/config/terminal
		[HttpGet("terminal")]
		public async Task<IActionResult> GetTerminalConfigFile (CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(WebSettings.TerminalConfigFilePath)) return BadRequest("No TerminalConfigFilePath specified.");

			if (!System.IO.File.Exists(WebSettings.TerminalConfigFilePath) && WebSettings.DatabaseVersion > 1) {
				return await GetTerminalConfig(HttpContext.GetOrg(), ct).ConfigureAwait(false);
			}

			string text;

			using (var stream = System.IO.File.OpenText(WebSettings.TerminalConfigFilePath)) {
				text = await stream.ReadToEndAsync().ConfigureAwait(false);
			}

			var n1 = text.IndexOf('{');
			var n2 = text.LastIndexOf('}');

			if (n1 < 0 || n2 < 0 || n1 >= n2) return BadRequest("Config file is not proper JSON.");

			return Content(text.Substring(n1, n2 - n1 + 1));
		}

		// http://url/config/terminal/{org}
		[HttpGet("terminal/{orgId}")]
		public async Task<IActionResult> GetTerminalConfig (string orgId, CancellationToken ct)
		{
			if (!HttpContext.GetOrg().Equals(orgId, StringComparison.OrdinalIgnoreCase)) return StatusCode(403);

			using (var db = new ConfigDB()) {
				var config = await db.TerminalConfigs.SingleOrDefaultAsync(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase), ct).ConfigureAwait(false);
				if (config == null) return NotFound();

				return Content(config.Text);
			}
		}

		// http://url/config/terminal
		[HttpPost("terminal")]
		[Authorize(Roles = nameof(Filters.All))]
		public async Task<IActionResult> UpdateTerminalConfigFile ([FromBody] JToken json, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(WebSettings.TerminalConfigFilePath)) return BadRequest("No TerminalConfigFilePath specified.");

			if (!System.IO.File.Exists(WebSettings.TerminalConfigFilePath) && WebSettings.DatabaseVersion > 1) {
				await UpdateTerminalConfig(HttpContext.GetOrg(), json, ct).ConfigureAwait(false);
				return NoContent();
			}

			var text = "var Config = " + json.ToString();

			using (var stream = new StreamWriter(WebSettings.TerminalConfigFilePath, false, Encoding.UTF8)) {
				await stream.WriteAsync(text).ConfigureAwait(false);
			}

			return NoContent();
		}

		// http://url/config/terminal/{org}
		[HttpPost("terminal/{orgId}")]
		[Authorize(Roles = nameof(Filters.All))]
		public async Task<IActionResult> UpdateTerminalConfig (string orgId, [FromBody] JToken json, CancellationToken ct)
		{
			using (var db = new ConfigDB()) {
				var config = await db.TerminalConfigs.SingleOrDefaultAsync(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase), ct).ConfigureAwait(false);
				if (config == null) return NotFound();

				config.Text = json.ToString();
				config.Modified = DateTime.Now;

				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return NoContent();
			}
		}
	}

	public partial class TerminalScriptController : Microsoft.AspNetCore.Mvc.Controller
	{
		private const string TerminalConfigRoute = @"terminal\config\";

		[HttpGet("terminal/config/{orgScript}")]
		public async Task<IActionResult> GetTerminalConfigFile (string orgScript, CancellationToken ct)
		{
			if (orgScript.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) orgScript = orgScript.Substring(0, orgScript.Length - 3);
			if (string.IsNullOrWhiteSpace(orgScript)) return BadRequest($"Invalid script name: {orgScript}");

			if (string.IsNullOrWhiteSpace(WebSettings.TerminalConfigFilePath)) return BadRequest("No TerminalConfigFilePath specified.");

			var filepath = Path.Combine(WebSettings.WwwRootPath, TerminalConfigRoute) + orgScript + ".js";

			if (filepath.Equals(WebSettings.TerminalConfigFilePath, StringComparison.OrdinalIgnoreCase) &&
					System.IO.File.Exists(WebSettings.TerminalConfigFilePath)) {

				using (var stream = System.IO.File.OpenText(WebSettings.TerminalConfigFilePath)) {
					var script = await stream.ReadToEndAsync().ConfigureAwait(false);
					return Content(script, "application/javascript", Encoding.UTF8);
				}
			} else if (WebSettings.DatabaseVersion >= ConfigDB.Version_TermialConfig) {
				using (var db = new ConfigDB(WebSettings.DatabaseSchema, WebSettings.DatabaseVersion)) {
					var config = await db.TerminalConfigs.SingleOrDefaultAsync(c => c.OrgId.Equals(orgScript, StringComparison.OrdinalIgnoreCase), ct).ConfigureAwait(false);
					if (config == null) return NotFound();

					var script = "var Config = " + config.Text + ";";

					return Content(script, "application/javascript", Encoding.UTF8);
				}
			} else {
				return NotFound();
			}
		}
	}
}