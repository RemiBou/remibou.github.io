---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor, Google, OAuth, ASPNET Core]
---
# Implementing Google OAuth with Blazor (0.4) and ASPNET Core 2.1.1

ASPNET Core project template provides everything for quickly implementing OAuth via various providers. But these templates are based on ASPNET Core MVC. In this blog post I'll explain how I changed the code for implementing Google authentication on a Blazor App.

My solution([TOSS](https://github.com/RemiBou/Toss.Blazor)) has 3 projects :
- Client : the blazor code (librairy targeting netstandard 2.0)
- Server : ASPNET Core  app serving RPC via HTTP  (app targeting netcoreapp2.1)
- Shared : shared class between client and server  (librairy targeting netstandard 2.0)

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

- For getting the configuration values please visit <https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-2.1&tabs=aspnetcore2x>
- The first line adds all the services needed by ASPNET Core  (you won't need any of those directly but you can find the detail [here](https://github.com/aspnet/Security/blob/648bb1e8101beb6d0f2d8069a0b57e165318a52a/src/Microsoft.AspNetCore.Authentication/AuthenticationServiceCollectionExtensions.cs)
- The second line adds google as an OAuth provider [source code here](https://github.com/ume05rw/AspNetCore.WithFrameworkSource.All.2.0/blob/82ee05dd041aa93cfe4ba07b74dd4d8d1d68b1de/AspNetCore/Security/src/Microsoft.AspNetCore.Authentication.Google/GoogleExtensions.cs)

Then you need to setup the authentication middleware in Configure

```cs
    app.UseAuthentication();
```

- You have to put it BEFORE the "UseMvc()"

## Listing Providers
Now everything is setup let's start with the first step of this process : listing the authentication providers the user can use.

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

This action will be called by ajax on your client side. Your blazor page will look like this

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

- This calls the action we just created and bind the result to the loginProviders local field, blazor will take care of the binding with the html
- I'm not sure StateHasChanged() is needed here 
- A button list with each providers will be displayed (only Google at this point)
- This is one of the only traditionnal form post in the app as the next step will be hard to implement in ajax, and in a UX point of view it doesn't change anything (user browser is redirected)

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

- The first line is the link on which the user will be redirected with his token (it will be decyphered and will give me the user's email so I can auth him).
- The Challenge result will return a 401 to the user browser with the login page url (google's).

## Landing page

At this point, the user is on google login page he enters his credentials and accept to give some informations to the application you declared in google console. For now we just ask for the email and name, but you can ask for other informations about your user as well.

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
- We still have to create a local account for this user, so we redirect him to "/account/externalLogin". The user account in our db will be created at this time. 
- If the user is logged in, we can redirect him to the homepage, the property User in our Controller will be filled with his informations.

## Account creation

This step is built with a razor page like this

```cs
@page "/account/externalLogin"
@inject IHttpClient HttpClient;
@inject IUriHelper UriHelper
<h4>Associate your @model.Provider account.</h4>
<hr />

<p class="text-info">
    You've successfully authenticated with <strong>@model.Provider</strong>.
    Please enter an email address and a login for this site below and click the Register button to finish
    logging in.
</p>

<div class="row">
    <div class="col-md-4">
        <form method="post" >
            <div class="form-group">
                <label for="Email">Email</label>
                <input id="Email" bind="@model.Email" class="form-control" />
            </div>           
            <button type="button" onclick="@ExternalLoginConfirm" class="btn btn-default">Register</button>
        </form>
    </div>
</div>


@functions {

    ExternalLoginConfirmationCommand model = new ExternalLoginConfirmationCommand();
    protected override async Task OnInitAsync()
    {
        var response = await Http.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/account/externalLoginDetails"));
        model = JsonUtil.Deserialize<ExternalLoginConfirmationCommand>(await response.Content.ReadAsStringAsync());
        StateHasChanged();
    }
    async Task ExternalLoginConfirm()
    {
        var requestJson = JsonUtil.Serialize(model);
        await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/account/externalLoginConfirmation")
        {
            Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json")
        });
        UriHelper.NavigateTo("/");
    }
}
```

- I call with a get externalLoginDetails for prefilling the form with the user's email address
- Then I post the content of the form. I don't handle error here, if you want to see how I manage validation, please go to my repo.
- UriHelper.NavigateTo("/") send the user to the home page.

Here is the content of the action externalLoginConfirmation

```cs
[HttpPost, AllowAnonymous]
public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationCommand command)
{
     // Get the information about the user from the external login provider
    var info = await _signInManager.GetExternalLoginInfoAsync();
    if (info == null)
    {
        throw new ApplicationException("Error loading external login information during confirmation.");
    }
    var user = new ApplicationUser { UserName = command.Email, Email = command.Email, EmailConfirmed = true };
    var result = await _userManager.CreateAsync(user);
    if (result.Succeeded)
    {
        result = await _userManager.AddLoginAsync(user, info);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Ok();
        }
    }
    return BadRequest();
}
```

- I force EmailConfirmed to true, as I trust google on this, maybe I should use "info" instead of the command
- This creates the user and add login information to him, you can have multiple external providers for one user but I won't use it here as I think it a really edge case.

## Sources

- <https://andrewlock.net/introduction-to-authorisation-in-asp-net-core/>
- <https://github.com/aspnet/Security>
- <https://blazor.net>

