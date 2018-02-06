using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using iChen.OpenProtocol;
using iChen.Persistence.Server;

namespace iChen.Web
{
	/// <summary>
	/// Generic Basic Authentication filter that checks for basic authentication
	/// headers and challenges for authentication if no authentication is provided
	/// Sets the Thread Principle with a GenericAuthenticationPrincipal.
	///
	/// You can override the OnAuthorize method for custom authorization logic that
	/// might be application specific.
	///
	/// This code is from http://weblog.west-wind.com/posts/2013/Apr/18/A-WebAPI-Basic-Authentication-Authorization-Filter
	/// </summary>
	/// <remarks>
	/// Always remember that Basic Authentication passes username and passwords
	/// from client to server in plain text, so make sure SSL is used with basic authentication
	/// to encode the Authorization header on all requests (not just the login).
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
	public class BasicAuthenticationAttribute : AuthorizationFilterAttribute
	{
		private bool m_RequireAuthentication = true;
		private Filters m_RequiredFilters = Filters.All;
		private string m_RequiredOrg = null;
		private bool m_Active = true;

		public BasicAuthenticationAttribute (bool RequireAuthentication = true, Filters RequiredFilters = Filters.All, string RequiredOrg = null)
		{
			if (RequiredOrg != null && string.IsNullOrWhiteSpace(RequiredOrg)) throw new ArgumentNullException(nameof(RequiredOrg));

			m_RequireAuthentication = RequireAuthentication;
			m_RequiredFilters = RequiredFilters;
			m_RequiredOrg = RequiredOrg;
		}

		/// <summary>
		/// Overridden constructor to allow explicit disabling of this
		/// filter's behavior. Pass false to disable (same as no filter
		/// but declarative)
		/// </summary>
		/// <param name="active"></param>
		public BasicAuthenticationAttribute (bool active)
		{
			m_Active = active;
		}

		/// <summary>
		/// Override to Web API filter method to handle Basic Authentication check
		/// </summary>
		/// <param name="actionContext"></param>
		public override void OnAuthorization (HttpActionContext actionContext)
		{
			if (m_Active) {
				UserIdentity identity;

				try {
					identity = ParseAuthorizationHeader(actionContext);

					if (identity == null) {
						Thread.CurrentPrincipal = null;
						if (HttpContext.Current != null) HttpContext.Current.User = null;

						if (m_RequireAuthentication) {
							actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized, $"Not logged in.");
							AddChallenge(actionContext);
						} else {
							base.OnAuthorization(actionContext);
						}

						return;
					}
				} catch (UnauthorizedAccessException ex) {
					// Do not send challenge when simply unauthorized
					Thread.CurrentPrincipal = null;
					if (HttpContext.Current != null) HttpContext.Current.User = null;
					actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Forbidden, ex.Message);
					return;
				}

				var principal = new GenericPrincipal(identity, identity.Session.Filters.ToString().Split(',').Select(x => x.Trim()).ToArray());

				Thread.CurrentPrincipal = principal;

				// Inside of ASP.NET this is required
				if (HttpContext.Current != null) HttpContext.Current.User = principal;

				base.OnAuthorization(actionContext);
			}
		}

		/// <summary>
		/// Parses the Authorization header
		/// </summary>
		/// <param name="actionContext"></param>
		public virtual UserIdentity ParseAuthorizationHeader (HttpActionContext actionContext)
		{
			string authHeader = null;
			var auth = actionContext.Request.Headers.Authorization;
			if (auth != null && auth.Scheme == "Basic") authHeader = auth.Parameter;

			if (string.IsNullOrWhiteSpace(authHeader)) {
				// No Authorization header - check cookies
				var cookie = actionContext.Request.Headers.GetCookies(SessionStore.SessionCookieID);
				if (cookie == null) return null;
				authHeader = cookie.SelectMany(c => c.Cookies).FirstOrDefault(c => c.Name == SessionStore.SessionCookieID)?.Value;
				if (string.IsNullOrWhiteSpace(authHeader)) return null;
			} else {
				// Authorization header - check if it is from a challenge
				try {
					var credentials = Encoding.Default.GetString(Convert.FromBase64String(authHeader));

					// username:password
					var tokens = credentials.Split(':');
					if (tokens.Length >= 2) {
						var username = tokens[0].Trim();
						var password = tokens[1].Trim();

						if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) {
							// Login to the user
							authHeader = SessionStore.Login(username, password);
							if (authHeader == null) return null;
						}
					}
				} catch (FormatException) { }
			}

			authHeader = authHeader.Trim();

			var user = SessionStore.GetSession(authHeader);
			if (user == null) return null;

			// Check access

			if (m_RequireAuthentication) {
				if (m_RequiredFilters != Filters.None && !user.Filters.HasFlag(m_RequiredFilters)) throw new UnauthorizedAccessException($"The user does not have enough authority for this request.");
				if (m_RequiredOrg != null && !m_RequiredOrg.Equals(user.OrgId ?? DataStore.DefaultOrgId, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException($"The user does not have enough authority for this request.");
			}

			return new UserIdentity(user);
		}

		/// <summary>
		/// Send the Authentication Challenge request
		/// </summary>
		/// <param name="message"></param>
		/// <param name="actionContext"></param>
		private void AddChallenge (HttpActionContext actionContext)
		{
			var host = actionContext.Request.RequestUri.DnsSafeHost;
			actionContext.Response.Headers.Add("WWW-Authenticate", $"Basic realm=\"{host}\"");
		}
	}

	public class UserIdentity : GenericIdentity
	{
		public UserSession Session { get; set; }

		public UserIdentity (UserSession session) : base(session?.Name, "Basic")
		{
			if (session == null) throw new ArgumentNullException(nameof(session));
			Session = session;
		}
	}
}