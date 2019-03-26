using System;
using System.Collections.Generic;
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

		public class MoldX : Mold
		{
			public MoldX () : base()
			{
			}

			public MoldX (Mold mold) : base()
			{
				this.ID = mold.ID;
				this.Guid = mold.Guid;
				this.Name = mold.Name;
				base.IsEnabled = mold.IsEnabled;
				this.ControllerId = mold.ControllerId;
				this.Created = mold.Created;
				this.Modified = mold.Modified;

				var data = mold.MoldSettings.OrderBy(s => s.Offset).ToList();
				if (data.Count > 0) {
					var list = new ushort[data[data.Count - 1].Offset + 1];
					foreach (var s in data) { list[s.Offset] = s.RawData; }
					this.Settings = RunLengthEncoder.Encode(list);
					this.NumSettings = list.Length;
				}
			}

			public Mold GetBase ()
			{
				var mx = new Mold()
				{
					ID = base.ID,
					Guid = base.Guid,
					Name = base.Name,
					IsEnabled = base.IsEnabled,
					ControllerId = base.ControllerId,
					Created = base.Created,
					Modified = base.Modified
				};

				if (Settings != null && Settings.Count > 0) {
					var data = RunLengthEncoder.Decode(Settings);
					for (var x = 0; x < data.Count; x++) {
						if (x < data.Count - 1 && data[x] == 0) continue;
						mx.MoldSettings.Add(new MoldSetting() { Mold = mx, Offset = (short) x, RawData = data[x] });
					}
				}

				return mx;
			}

			[JsonIgnore]
			public bool? m_IsEnabled = null;

			[JsonProperty("isEnabled", DefaultValueHandling = DefaultValueHandling.Include, Order = 98)]
			public new bool? IsEnabled
			{
				get { return base.IsEnabled; }
				set {
					m_IsEnabled = value;
					if (value.HasValue) base.IsEnabled = value.Value;
				}
			}

			[JsonProperty(Order = 98)]
			public int NumSettings { get; private set; }

			[JsonProperty(Order = 99)]
			public IList<int> Settings { get; set; }
		}

		#endregion Data class

		// http://url/config/molds
		[HttpGet("molds")]
		public async Task<IActionResult> GetMoldsAsync (CancellationToken ct)
		{
			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var mx = await db.Molds.AsNoTracking()
													.Where(m => m.Controller.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
													.GroupBy(m => m.Name)
													.ToDictionaryAsync(g => g.Key, g => g.ToDictionary(x => x.ControllerId.GetValueOrDefault(0), x => new MoldX(x)) as IReadOnlyDictionary<int, MoldX>)
													.ConfigureAwait(false);

				return Ok(mx);
			}
		}

		// http://url/config/molds/{id}
		[HttpGet("molds/{id}")]
		public async Task<IActionResult> GetMoldAsync (int id, CancellationToken ct)
		{
			if (id <= 0) return NotFound();

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var mold = await db.Molds.AsNoTracking().Include(m => m.MoldSettings)
														.Where(m => m.Controller.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.SingleOrDefaultAsync(m => m.ID == id, ct)
														.ConfigureAwait(false);

				if (mold == null) return NotFound();

				return Ok(new MoldX(mold));
			}
		}

		// http://url/config/molds - Add
		[HttpPost("molds")]
		[Authorize(Roles = nameof(Filters.Mold))]
		public async Task<IActionResult> AddMoldAsync ([FromBody] MoldX mold, CancellationToken ct)
		{
			if (mold.ID != 0) return BadRequest("ID must be set to zero.");

			if (string.IsNullOrWhiteSpace(mold.Name)) return BadRequest($"Invalid mold name: [{mold.Name}].");
			if (mold.ControllerId.HasValue && mold.ControllerId <= 0) return BadRequest($"Invalid controller: {mold.ControllerId}.");

			mold.Name = mold.Name.Trim();
			mold.Modified = null;

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				if (mold.ControllerId.HasValue) {
					var ctrl = await db.Controllers
																.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
																.SingleOrDefaultAsync(c => c.ID == mold.ControllerId, ct)
																.ConfigureAwait(false);

					if (ctrl == null) return NotFound();
				}
				var cx = await db.Molds.AsNoTracking()
														.Where(m => m.ControllerId == mold.ControllerId)
														.SingleOrDefaultAsync(m => m.Name.Equals(mold.Name, StringComparison.OrdinalIgnoreCase), ct)
														.ConfigureAwait(false);

				if (cx != null) return BadRequest($"Controller/Mold [{mold.ControllerId}/{mold.Name}] already exists.");

				var mx = mold.GetBase();

				db.Molds.Add(mx);
				db.MoldSettings.AddRange(mx.MoldSettings);

				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Created($"molds/{mx.ID}", new MoldX(mx));
			}
		}

		// http://url/config/molds/{id} - Update
		[HttpPost("molds/{id}")]
		[Authorize(Roles = nameof(Filters.Mold))]
		public async Task<IActionResult> UpdateMoldAsync (int id, [FromBody] MoldX delta, CancellationToken ct)
		{
			if (id <= 0) return NotFound();
			if (delta.ID != 0) return BadRequest($"ID cannot be changed.");

			if (delta.Name != null) {
				if (string.IsNullOrWhiteSpace(delta.Name)) return BadRequest($"Invalid name: [{delta.Name}].");
				delta.Name = delta.Name.Trim();
			}

			if (delta.ControllerId.HasValue) {
				if (delta.ControllerId < 0) return BadRequest($"Invalid controller: {delta.ControllerId}.");
			}

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var mold = await db.Molds
														.Where(m => m.Controller.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.SingleOrDefaultAsync(m => m.ID == id, ct)
														.ConfigureAwait(false);

				if (mold == null) return NotFound();

				if (delta.m_IsEnabled.HasValue) mold.IsEnabled = delta.IsEnabled.Value;

				if (delta.ControllerId.HasValue) {
					var ctrl = await db.Controllers.AsNoTracking()
																	.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
																	.SingleOrDefaultAsync(c => c.ID == delta.ControllerId, ct)
																	.ConfigureAwait(false);

					if (ctrl == null) return BadRequest($"Invalid controller: {delta.ControllerId}");

					mold.ControllerId = delta.ControllerId;
				}

				if (delta.Name != null) {
					var cx = await db.Molds.AsNoTracking()
															.Where(m => m.ControllerId == mold.ControllerId)
															.SingleOrDefaultAsync(m => m.Name.Equals(mold.Name, StringComparison.OrdinalIgnoreCase), ct)
															.ConfigureAwait(false);

					if (cx != null) return BadRequest($"Controller/Mold [{mold.ControllerId}/{mold.Name}] already exists.");

					mold.Name = delta.Name;
				}

				if (delta.Guid != default(Guid)) mold.Guid = delta.Guid;
				mold.Modified = DateTime.Now;

				// Replace settings data?
				if (delta.Settings != null) {
					await db.Entry(mold).Collection(m => m.MoldSettings).LoadAsync(ct).ConfigureAwait(false);
					db.MoldSettings.RemoveRange(mold.MoldSettings.AsEnumerable());

					var rawsettings = RunLengthEncoder.Decode(delta.Settings);

					for (var x = 0; x < rawsettings.Count; x++) {
						// Make sure the last item is always stored to keep the accurate length of the whole data set
						if (x >= rawsettings.Count - 1 || rawsettings[x] != 0) db.MoldSettings.Add(new MoldSetting() { MoldId = mold.ID, Offset = (short) x, RawData = rawsettings[x] });
					}
				}

				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Ok(new MoldX(mold));
			}
		}

		// http://url/config/molds/{id}
		[HttpDelete("molds/{id}")]
		[Authorize(Roles = nameof(Filters.Mold))]
		public async Task<IActionResult> DeleteMoldAsync (int id, CancellationToken ct)
		{
			if (id <= 0) return NotFound();

			var orgId = HttpContext.GetOrg();

			using (var db = new ConfigDB()) {
				var mold = await db.Molds
														.Where(m => m.Controller.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.SingleOrDefaultAsync(m => m.ID == id, ct)
														.ConfigureAwait(false);

				if (mold == null) return NotFound();

				await db.Entry(mold).Collection(m => m.MoldSettings).LoadAsync(ct).ConfigureAwait(false);

				var mx = new MoldX(mold);

				db.MoldSettings.RemoveRange(mold.MoldSettings);
				db.Molds.Remove(mold);
				await db.SaveChangesAsync(ct).ConfigureAwait(false);

				return Ok(mx);
			}
		}
	}
}