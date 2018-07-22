# CSRF protection on a ASPNET Core / Blazor app

CSRF is a well know web app vulnerability, you canf ind more about it here <CSRF is a well know web app vulnerability, you canf ind more about it here <https://scotthelme.co.uk/csrf-is-dead/>. As you can see on the title, this vulerability is dead. But it's dead for everyone with an up to date browser. So we have to implmeent classic protections until everybrowser has implemented the protection and everyone has updated his browser.

In this article I'll show you how I implemented it with my Blazor / ASPNET Core app calles [TOSS](https://github.com/RemiBou/Toss.Blazor)

## Server side (ASPNET Core 2.1.1)
Usually CSRF protection works this way :
- browser renders a form with a token in an hidden field
- user submit the form
- server validate the field is on the client request and validate it

But in a SPA, forms are not created on server side so we need an other way. The one I'll use is the following :
- Server sends a non http validation cookie
- client read the cookie and will send its value in an header for every http request (ajax) it'll do

ON the server side I need an ASPNET Core middleware for setting the cokie if not present

```cs
    public class CsrfTokenCOokieMiddleware
    {
        private readonly IAntiforgery _antiforgery;
        private readonly RequestDelegate _next;

        public CsrfTokenCOokieMiddleware(IAntiforgery antiforgery, RequestDelegate next)
        {
            _antiforgery = antiforgery;
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if(context.Request.Cookies["CSRF-TOKEN"] == null)
            {
                var token = _antiforgery.GetTokens(context);
                context.Response.Cookies.Append("CSRF-TOKEN", token.RequestToken, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = false });
            }
            await _next(context);
        }
    }
```

- Middleware doesn't have to implement a specific interface, just declare this method "async Task InvokeAsync(HttpContext context)", don't know why the .Net core team didn't securized it with an interface though
- I use the built-in IAntiforgery service for generating the value as I have no idea how it's supposed to be built. When we are talking about security we have to avoid as much as we can, in-house code.

Then I register this Middleware in my Configure method on Startup.cs

```cs
 app.UseMiddleware<CsrfTokenCOokieMiddleware>();
```

And enable XSRF protection via headers on the "ConfigureService" method

```cs
 services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
            });
```

- If you don't add this, the CSRF protection will only look for field on the request

In all the method I want to protect (mostly POST, as GET have no impact on the system) I add the following attribute

```cs
[ValidateAntiForgeryToken]
```

- There might be a way to avoid this for all methods but I don't know it.

## Client Side (Blazor 0.4)

On the client side, first I need to read the cookie's value (both are hosted on the same domain, so when I load my app the cookie is supposed to be set). FOr reading the cokie value I'll have to use Js interop because there is no method in Blazor for reading the cookies.

```js
Blazor.registerFunction("getDocumentCookie", function () {
    return { content: document.cookie };
});
```

- I use a container for my result because the string serialization/deserialization in Blazor is buggy

Then in C# is read this value 

```cs
 public static string GetCookie()
        {
            StringHolder stringHolder = RegisteredFunction.Invoke<StringHolder>("getDocumentCookie");           
            return stringHolder.Content;
        }
```

I created a service for parsing this value

```cs
 /// <summary>
    /// Service for reading the current cookie on the browser
    /// </summary>
    public class BrowserCookieService : IBrowserCookieService
    {
        /// <summary>
        /// returns the cookie value or null if not set
        /// </summary>
        /// <param name="cookieName"></param>
        /// <returns></returns>
        public string Get(Func<string,bool> filterCookie)
        {
            return JsInterop
                .GetCookie()
                .Split(';')
                .Select(v => v.TrimStart().Split('='))
                .Where(s => filterCookie(s[0]))
                .Select(s => s[1])
                .FirstOrDefault();
        }
    }
```

The DI settings are set in Program.cs n my client project like this

```cs
configure.Add(new ServiceDescriptor(
   typeof(IBrowserCookieService),
   typeof(BrowserCookieService),
   ServiceLifetime.Singleton));
```

Then I add the headers on all my http calls like this(I use a wrapper around HttpClient for avoiding copy/paste)

```cs
 private HttpRequestMessage PrepareMessage(HttpRequestMessage httpRequestMessage)
  {
      string csrfCookieValue = browserCookieService.Get(c => c.Equals("CSRF-TOKEN"));
      if (csrfCookieValue != null)
          httpRequestMessage.Headers.Add("X-CSRF-TOKEN", csrfCookieValue);
      return httpRequestMessage;
  }
```

- I can use HttpClient DefaultHeaders but I couldn't find a nice place to initialize it.
- And it works :)

I think this way of implementing CSRF is quite usual for SPA, but if there is a security problem please tell me.

## Reference

- <https://scotthelme.co.uk/csrf-is-dead/>
- <https://github.com/aspnet/Antiforgery/>
- <https://docs.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-2.1#javascript-ajax-and-spas>
