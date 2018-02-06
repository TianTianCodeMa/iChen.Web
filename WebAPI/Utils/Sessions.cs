using System;
using System.Collections.Generic;
using System.Linq;
using iChen.OpenProtocol;
using iChen.Persistence.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace iChen.Web
{
	public class UserSession : User
	{
		public string SessionID { get; }
		public DateTime Started { get; } = DateTime.Now;
		public DateTime LastAccessed { get; set; } = DateTime.Now;
		public string[] Roles { get; }

		public UserSession (string sessionId, User user)
		{
			if (user == null) throw new ArgumentNullException(nameof(user));

			this.SessionID = sessionId;
			this.ID = user.ID;
			this.OrgId = user.OrgId;
			this.Password = user.Password;
			this.Name = user.Name;
			this.IsEnabled = user.IsEnabled;
			this.Filters = user.Filters;
			this.AccessLevel = user.AccessLevel;
			this.Created = user.Created;
			this.Modified = user.Modified;

			this.Roles = Enum.GetNames(typeof(Filters)).Where(key => key != nameof(Filters.None)).Where(key => {
				if (!Enum.TryParse<Filters>(key, out var val)) return false;
				return user.Filters.HasFlag(val);
			}).ToArray();
		}

		[JsonIgnore]
		public new string OrgId { get { return base.OrgId; } set { base.OrgId = value; } }
	}

	public static class Sessions
	{
		public const string SessionCookieID = "api_key";
		public static int TimeOut = 15 * 60 * 1000;    // Time-out the session after 15 min of inactivity
		private static readonly Dictionary<string, UserSession> m_Sessions = new Dictionary<string, UserSession>(StringComparer.OrdinalIgnoreCase);


		private static void PruneSessions ()
		{
			lock (m_Sessions) {
				while (true) {
					var expired = m_Sessions.FirstOrDefault(ss => (DateTime.Now - ss.Value.LastAccessed).TotalMilliseconds > TimeOut);
					if (expired.Key == null) break;

					m_Sessions.Remove(expired.Key);
				}
			}
		}

		public static string Login (string username, string password)
		{
			PruneSessions();

			if (string.IsNullOrWhiteSpace(username)) return null;
			if (string.IsNullOrWhiteSpace(password)) return null;

			username = username.Trim();
			password = password.Trim();

			// Get org ID if present: username can be "orgId\user"
			var orgId = DataStore.DefaultOrgId;
			var n = username.IndexOf('\\');

			if (n > 0 && n < username.Length - 1) {
				orgId = username.Substring(0, n).Trim();
				username = username.Substring(n + 1).Trim();
			}

			User user;

			using (var db = new ConfigDB()) {
				user = db.Users.AsNoTracking()
										.Where(ux => ux.IsEnabled)
										.Where(ux => ux.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
										.SingleOrDefault(ux => ux.Name.Equals(username, StringComparison.OrdinalIgnoreCase) && ux.Password.Equals(password, StringComparison.OrdinalIgnoreCase));

				if (user == null) return null;
			}

			lock (m_Sessions) {
				// Already logged in?
				var existing = m_Sessions.Values
										.Where(ss => ss.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
										.Where(ss => ss.Name.Equals(username, StringComparison.OrdinalIgnoreCase))
										.ToList();

				// Remove existing login's
				foreach (var ux in existing) m_Sessions.Remove(ux.SessionID);

				// New login
				var guid = Guid.NewGuid().ToString().Replace("-", "").ToUpperInvariant();
				m_Sessions[guid] = new UserSession(guid, user);
				return guid;
			}
		}

		public static bool Logout (string token)
		{
			PruneSessions();

			if (string.IsNullOrWhiteSpace(token)) return false;
			token = token.Trim();

			lock (m_Sessions) {
				if (!m_Sessions.ContainsKey(token)) return false;
				m_Sessions.Remove(token);
				return true;
			}
		}

		public static ICollection<UserSession> GetAllSessions ()
		{
			PruneSessions();
			return m_Sessions.Values.ToList();
		}

		public static UserSession GetSession (string token)
		{
			PruneSessions();

			if (string.IsNullOrWhiteSpace(token)) return null;
			token = token.Trim();

			lock (m_Sessions) {
				if (!m_Sessions.ContainsKey(token)) return null;
				var session = m_Sessions[token];
				session.LastAccessed = DateTime.Now;
				return session;
			}
		}

		public static UserSession GetCurrentUser (HttpRequest Request)
		{
			if (!Request.Cookies.ContainsKey(SessionCookieID)) return null;

			return GetSession(Request.Cookies[SessionCookieID]);
		}

		public static bool IsAuthorized (HttpRequest Request, out string orgId, Filters filters = Filters.All)
		{
			orgId = null;

			var ux = GetCurrentUser(Request);
			if (ux == null) return false;

			orgId = ux.OrgId ?? DataStore.DefaultOrgId;

			if (filters == Filters.None) return true;
			return ux.Filters.HasFlag(filters);
		}
	}
}