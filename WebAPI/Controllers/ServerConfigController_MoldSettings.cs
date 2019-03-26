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

		public class MoldSettingX : MoldSetting
		{
			public MoldSettingX () : base()
			{
			}

			public MoldSettingX (MoldSetting ms) : base()
			{
				this.Offset = ms.Offset;
				base.RawData = ms.RawData;
				this.Created = ms.Created;
				this.Modified = ms.Modified;
			}

			public MoldSetting GetBase ()
			{
				return new MoldSetting()
				{
					MoldId = base.MoldId,
					Offset = base.Offset,
					RawData = base.RawData,
					Variable = base.Variable,
					Created = base.Created,
					Modified = base.Modified
				};
			}

			[JsonIgnore]
			public new short Value { get; set; }

			[JsonIgnore]
			public ushort? m_RawData = null;

			[JsonProperty(PropertyName = "value", Order = 99)]
			public new ushort? RawData
			{
				get { return base.RawData; }
				set {
					m_RawData = value;
					if (value.HasValue) base.RawData = value.Value;
				}
			}
		}

		#endregion Data class

		// http://url/config/molds/{moldId}/settings
		[HttpGet("molds/{moldId}/settings")]
		[Authorize(Roles = nameof(Filters.Mold))]
		public async Task<IActionResult> GetMoldSettingsAsync (int moldId, CancellationToken ct, bool expand = false)
		{
			if (moldId <= 0) return NotFound();

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var mold = await db.Molds.AsNoTracking().Include(m => m.MoldSettings)
														.Where(m => m.Controller.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.SingleOrDefaultAsync(m => m.ID == moldId, ct)
														.ConfigureAwait(false);

				if (mold == null) return NotFound();

				if (!expand) return Ok(mold.MoldSettings.ToDictionary(s => s.Offset, s => (ushort) s.Value));

				var rawdata = new ushort[mold.MoldSettings.Max(ms => ms.Offset) + 1];

				foreach (var ms in mold.MoldSettings) rawdata[ms.Offset] = ms.RawData;

				return Ok(rawdata);
			}
		}

		// http://url/config/molds/{moldId}/settings/{offset} - Add
		[HttpPost("molds/{moldId}/{offset}")]
		[Authorize(Roles = nameof(Filters.Mold))]
		public async Task<IActionResult> AddMoldSettingAsync (int moldId, int offset, ushort value, int variable, CancellationToken ct)
		{
			if (moldId <= 0) return NotFound();
			if (offset < 0) return NotFound();

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var mold = await db.Molds.AsNoTracking().Include(m => m.MoldSettings)
														.Where(m => m.Controller.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.SingleOrDefaultAsync(m => m.ID == moldId, ct)
														.ConfigureAwait(false);

				if (mold == null) return NotFound();

				var mx = await db.MoldSettings.SingleOrDefaultAsync(s => s.MoldId == moldId && s.Offset == offset, ct).ConfigureAwait(false);
				if (mx != null) return BadRequest($"Mold/Offset [{moldId}/{offset}] already exists.");

				var ms = new MoldSetting()
				{
					MoldId = moldId,
					Offset = (short) offset,
					Created = DateTime.Now,
					RawData = value,
					Variable = variable
				};

				db.MoldSettings.Add(ms);
				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Created($"molds/{ms.MoldId}/settings", new MoldSettingX(ms));
			}
		}

		// http://url/config/molds/{moldId}/settings/{offset} - Update
		[HttpPost("molds/{moldId}/{offset}")]
		[Authorize(Roles = nameof(Filters.Mold))]
		public async Task<IActionResult> UpdateMoldSettingAsync (int moldId, int offset, [FromBody] MoldSettingX delta, CancellationToken ct)
		{
			if (moldId <= 0) return NotFound();
			if (offset < 0) return NotFound();
			if (delta.MoldId != 0) return BadRequest("Mold ID cannot be changed.");
			if (delta.Offset != 0) return BadRequest("Offset cannot be changed.");

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var mold = await db.Molds.AsNoTracking().Include(m => m.MoldSettings)
														.Where(m => m.Controller.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.SingleOrDefaultAsync(m => m.ID == moldId, ct)
														.ConfigureAwait(false);

				if (mold == null) return NotFound();

				var ms = await db.MoldSettings.SingleOrDefaultAsync(s => s.MoldId == moldId && s.Offset == offset, ct).ConfigureAwait(false);
				if (ms == null) return NotFound();

				if (delta.m_RawData.HasValue) ms.RawData = delta.RawData.Value;

				if (delta.Variable.HasValue) ms.Variable = (delta.Variable.Value == 0) ? (int?) null : delta.Variable.Value;

				delta.Modified = DateTime.Now;

				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Ok(new MoldSettingX(ms));
			}
		}

		// http://url/config/molds/{moldId}/settings/{offset}
		[HttpDelete("molds/{moldId}/{offset}")]
		[Authorize(Roles = nameof(Filters.Mold))]
		public async Task<IActionResult> DeleteMoldSettingAsync (int moldId, int offset, CancellationToken ct)
		{
			if (moldId <= 0) return NotFound();
			if (offset < 0) return NotFound();

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var mold = await db.Molds.AsNoTracking().Include(m => m.MoldSettings)
														.Where(m => m.Controller.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.SingleOrDefaultAsync(m => m.ID == moldId, ct)
														.ConfigureAwait(false);

				if (mold == null) return NotFound();

				var ms = await db.MoldSettings.SingleOrDefaultAsync(s => s.MoldId == moldId && s.Offset == offset, ct).ConfigureAwait(false);
				if (ms == null) return NotFound();

				db.MoldSettings.Remove(ms);
				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Ok(new MoldSettingX(ms));
			}
		}
	}
}