Asp.net core project template provide everything for quickly implementing OAuth via various providers. But these templates are based on aspnet core MVC. In this blog post I'll explain how I changed the code for implementing Google authentication on a Blazor App.

My app has 3 [TOSS](https://github.com/RemiBou/Toss.Blazor) librairies :
- Client : the blazor code
- Server : aspnet core app serving RPC via HTTP
- Shared : shared class between client and server

## Server configuration
You first need to configure the Google OAuth on your server application. Just add the following code in ConfigureServices

```cs
services.AddAuthentication()
        .AddGoogle(o =>
          {
              o.ClientId = Configuration["GoogleClientId"];
              o.ClientSecret = Configuration["GoogleClientSecret"];
          });
```

- For getting the parameters please visit <https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-2.1&tabs=aspnetcore2x>
- The first line adds all the services needed by aspnet (you won't need any of those directly but you can find the detail [here](https://github.com/aspnet/Security/blob/648bb1e8101beb6d0f2d8069a0b57e165318a52a/src/Microsoft.AspNetCore.Authentication/AuthenticationServiceCollectionExtensions.cs)
- The second line adds google as an OAuth provider [source code here](https://github.com/ume05rw/AspNetCore.WithFrameworkSource.All.2.0/blob/82ee05dd041aa93cfe4ba07b74dd4d8d1d68b1de/AspNetCore/Security/src/Microsoft.AspNetCore.Authentication.Google/GoogleExtensions.cs)

Then you need to setup the authentication middleware in Configure

```cs
    app.UseAuthentication();
```

- You have to put it BEFORE the "UseMvc"

## Listing Providers
Now everything is setu let's start with the first step of this process : listing the authentication providers the user can use.

On your AccountController add this action

```cs
 [HttpGet, AllowAnonymous]
        public async Task<IActionResult> LoginProviders()
        {
          return Ok((await _signInManager.GetExternalAuthenticationSchemesAsync())
                .Select(s => new SigninProviderViewModel()
                {
                    Name = s.Name,
                    DisplayName = s.DisplayName
                }));
        }
```

- The _signinManager needs to be injected in your controller (SignInManager<ApplicationUser>)

This action will be called by ajax on your client side, you will have two part, your blazor page will look like this

```cs
@page "/login"
<h1>Welcome</h1>
<div class="row">
    <div class="col-sm">
        <LoginExternal></LoginExternal>
    </div>
</div>
@inject HttpClient Http;
@inject IUriHelper UriHelper;
<h4>Use another service to log in.</h4>
@foreach (var provider in loginProviders)
{
    <form action="/api/account/externalLogin" method="post">
        <button type="submit"
                class="btn btn-default"
                name="provider"
                value="@provider.Name"
                title="@("Log in using your "+provider.DisplayName+" account")">
            @provider.Name
        </button>
    </form>
}
@functions{
    IEnumerable<SigninProviderViewModel> loginProviders = new SigninProviderViewModel[0];
    protected override async Task OnInitAsync()
    {
        var response = await Http.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/account/loginProviders"));
        loginProviders = JsonUtil.Deserialize<List<SigninProviderViewModel>>(await response.Content.ReadAsStringAsync());
        StateHasChanged();
    }
}
```

- This calls the action we just created and bind the result to the loginProviders local field, blazor will take care of the rest
- I'm not sure StateHasChanged() is needed here 
- A list of button with each providers will be displayed (only Google at this point)
- This is one of the only traditionnal form post in the app as the next step will be hard to implement in ajax, and in a UX point of view it doesn't change anything

## Redirection

Where the user clicks on a button it will do a POST to the following action

```cs
[HttpPost, AllowAnonymous]
public async Task<IActionResult> ExternalLogin([FromForm] string provider)
{
    // Request a redirect to the external login provider.
    var redirectUrl = _urlHelper.Action("ExternalLoginCallback", "Account");
    var properties = _signInManager.ConfigureExternalAuthenticationProperties(request.ProviderName, redirectUrl);
    return Challenge(properties, provider);
}
```
- The first line is the link on which the user will be redirected with his token (it will be decyphered and will give me the user's email so I can auth him)
- the Challenge result wil give instruction which will return a 401 to the user browser with the login page url (google's)

## Landing page

At this point, the user is on google login page he enters his credentials and accept to give some informations to the application you declared in google console. For now we just ask for the email and name, but you can ask for other informations about your suer as well.

For the landing page after this approval (which will be asked only once) the user will land on the following action

```cs
[HttpGet, AllowAnonymous]
public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
{
    if (remoteError != null)
    {
        return Redirect("/login");
    }
     var info = await _signInManager.GetExternalLoginInfoAsync();
    if (info == null)
    {
        return null;
    }

    // Sign in the user with this external login provider if the user already has a login.
    var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
    if (result == null || result.IsNotAllowed)
    {
        return Redirect("/login");
    }

    if (result.Succeeded)
    {
        return RedirectToLocal(returnUrl);
    }
    // If the user does not have an account, then ask the user to create an account.
    return Redirect("/account/externalLogin");
}

```

- The token is not a parameter of the action as there could be many providers, the GET parameters will be read by the signInManager in the httpcontext directly.



## Account creation

## Sources
- <https://andrewlock.net/introduction-to-authorisation-in-asp-net-core/>
