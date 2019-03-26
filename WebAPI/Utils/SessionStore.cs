using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace iChen.Web
{
	public class SessionStore : ITicketStore
	{
		private readonly IMemoryCache m_Store;

		public SessionStore (IMemoryCache cache) => m_Store = cache;

		public async Task<string> StoreAsync (AuthenticationTicket ticket)
		{
			var key = Guid.NewGuid().ToString().ToLowerInvariant().Replace("-", "");
			await RenewAsync(key, ticket);
			return key;
		}

		public Task RenewAsync (string key, AuthenticationTicket ticket)
		{
			// https://github.com/aspnet/Caching/issues/221
			// Set to "NeverRemove" to prevent undesired evictions from gen2 GC
			var options = new MemoryCacheEntryOptions
			{
				Priority = CacheItemPriority.NeverRemove
			};

			var expiry = ticket.Properties.ExpiresUtc;

			if (expiry.HasValue) options.SetAbsoluteExpiration(expiry.Value);

			//options.SetSlidingExpiration(TimeSpan.FromMinutes(60));

			m_Store.Set(key, ticket, options);

			return Task.CompletedTask;
		}

		public Task<AuthenticationTicket> RetrieveAsync (string key)
		{
			m_Store.TryGetValue(key, out AuthenticationTicket ticket);
			return Task.FromResult(ticket);
		}

		public Task RemoveAsync (string key)
		{
			m_Store.Remove(key);
			return Task.CompletedTask;
		}
	}
}
