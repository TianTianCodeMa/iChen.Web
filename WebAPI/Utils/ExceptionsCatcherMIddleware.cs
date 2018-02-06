using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace iChen.Web
{
	public class ExceptionCatchMiddleware
	{
		private readonly RequestDelegate _delegate;

		public ExceptionCatchMiddleware (RequestDelegate requestDelegate)
		{
			_delegate = requestDelegate;
		}

		public async Task Invoke (HttpContext context)
		{
			try {
				await _delegate(context);
			} catch (ReflectionTypeLoadException e) {
				foreach (Exception ex in e.LoaderExceptions) {
					Console.WriteLine($"{ex.Message} ---\n{ex.StackTrace}");
				}
			}
		}
	}
}
