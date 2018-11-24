# How to validate incoming command with reCaptcha on a Blazor app with a ASPNET Core backend with MediatR
The [Toss project](https://github.com/RemiBou/Toss.Blazor) is a message board : people register and then they send messages. One of the threat with this kind of application are robot : people creates program that creates new account and then post spam message. 

[reCaptcha](https://www.google.com/recaptcha/intro/v3.html) is a tool created by Google since a long time that aim to validate that a given action is done only by a human. In this blog post we'll see how I integrated this tool so it's easy for me to validate that a given action is done only by a human being.

## How repatcha works

Recaptcha works this way :
- a given action is done on your app
- you call their JS api for getting a token, this method will fail if the user is not a human (or at least reCaptcha does not think it's a robot). We completly ignore how it does it, but I trust google to be more eficient at this task than me and my 2 hours of work on this project per week.
- we send the token to the backend
- the backend send this token with our private key to Google reCaptcha API
- if the token is correct the API returns "succeed"
- if not then it returns an error and we can stop the processing of the query

## Getting the token

First thing we need to add reCaptcha script link :
```html
<script src='https://www.google.com/recaptcha/api.js?render=6LcySnsUAAAAAKFZn_ve4gTT5kr71EXVkQ_QsGot'></script>
```

- You can find this on your reCaptcha GUI
- it's sad google doesn't provide a version with integrity attribute so we're sure the script doesn't get modified by hackers

For getting the token on protected action I did it that way :

Every sensible command inherit from "NotARobot" class :
```cs
public class NotARobot
{
    public string Token { get; set; }
}
public class RegisterCommand : NotARobot, IRequest<CommandResult>
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; }

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    public string Name { get; set; }

    [Required]
    [StringLength(100,ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }
}
```
- This classes are defined in my shared assembly so I'll be able to read it on server side as well

Every time I send a command inheriting from NotARobot, I will gather a token.  This is done on my HttpClient extension

```cs
 public async Task Post<T>(T data)
        {
            if (data is NotARobot)
            {
                (data as NotARobot).Token =  await JSRuntime.Current.InvokeAsync<string>("runCaptcha",this._uri);
            }
            await ExecuteHttpQuery(async () =>
            {
                var requestJson = Json.Serialize(data);
                return await _httpClient.SendAsync(await PrepareMessageAsync(new HttpRequestMessage(HttpMethod.Post, _uri)
                {
                    Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json")
                }));
            });
        }
```
The js method called with interop is defined as such :
```js
runCaptcha = function (actionName) {
    return new Promise((resolve, reject) => {
        grecaptcha.ready(function () {
            grecaptcha.execute('6LcySnsUAAAAAKFZn_ve4gTT5kr71EXVkQ_QsGot', { action: actionName })
                .then(function (token) {
                    resolve(token);
                });
        });

    });
};
```
- the first argument is the public key given by Google, it's hard coded here because it's public and used only in one place
- I use Promise as it's the way to handle async execution in JSInterop
- Doing it this way I only need to add NotARobot to my command and the whole process will be done

Now that my server receives the token, I need to add the validation with Google API.

## Validating the token with MediatR

We can see on the previous source code that my class RegisterCommand also implement the IRequest interface from MediatR. This command is executed by a handle implementing IRequestHandler. MediatR provides an easy way for adding validation before the handler get executed : [pipelines](https://github.com/jbogard/MediatR/wiki/Behaviors).

Here is my pipeline code

```cs
public class CaptchaMediatRAdapter<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : NotARobot, IRequest<TResponse>
{
    private ICaptchaValidator captchaValidator;

    public CaptchaMediatRAdapter(ICaptchaValidator captchaValidator)
    {
        this.captchaValidator = captchaValidator;
    }


    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
    {
        await this.captchaValidator.Check(((NotARobot)request).Token);
        return await next();
    }
}
```

It's pretty simple it just calls my validator that is supposed to raise an exception when the validation fails. Here is my validator

```cs
public class CaptchaValidator : ICaptchaValidator
{
    private string _secret;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IHttpContextAccessor httpContextAccessor;

    public CaptchaValidator(string secret, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _secret = secret;
        this.httpClientFactory = httpClientFactory;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task Check(string token)
    {
        var webClient = this.httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("secret", _secret),
            new KeyValuePair<string, string>("response", token),
            new KeyValuePair<string, string>("remoteip", httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString())
        });
        var res = await webClient.PostAsync("https://www.google.com/recaptcha/api/siteverify",content);
        var resJson = JObject.Parse(await res.Content.ReadAsStringAsync());
        var succeed = resJson["success"].ToString() == "True";
        if (!succeed)
            throw new InvalidOperationException("Captcha validation failed : " + string.Join(",", resJson["error-codes"].AsEnumerable()));
    }
}
```
- we need to send the client IP
- secret is a ... secret and is defined in my secrets.json, you can find it on your reCaptcha GUI
- here I just parse the resulting json and see if the token is ok, if it's not then I raise an Exception. I could have returned a boolean, but this case is not supposed to happen beucase I only want human on my app.

And for injecting this service I do it like that on my ConfigureServices

```cs
services.AddHttpClient();
services.AddSingleton<ICaptchaValidator>(s => new CaptchaValidator(
          Configuration["GoogleCaptchaSecret"],                    
          s.GetRequiredService<IHttpClientFactory>(),
          s.GetRequiredService<IHttpContextAccessor>()));
```
- It's a singleton as there is no state
- Don't forget to init the HttpClient or IHttpClientFactory injection will fail

I also need to inject my pipeline, I don't know why MediatR ASPNET Core extension doesn't do this by default

```cs
services.AddScoped(typeof(IPipelineBehavior<, >), typeof(CaptchaMediatRAdapter<,>));
```

Now everything is bootstraped :
- A NotARobot is detected on client side and we try to get a token (this fails if the user is not a human)
- The token is send on the command payload
- MediatR executes the pipeline if the command inherit from NotARobot
- The validation will fail if the token is not correct 
- My command get executed if reCaptcha doesn't think it's a robot.

## E2E tests

Now my E2E tests are failing because Selenium WebDriver is a robot :(. 

I need to change the two validation process : on the client-side and server side.

On the client side, my test executes the following script

```cs
if (Browser is IJavaScriptExecutor)
{
    //in E2E test we disable getting the token from recaptcha
    ((IJavaScriptExecutor)Browser).ExecuteScript("runCaptcha = function(actionName) { return Promise.resolve('test'); }");
}
```
- The js function is overriden and returns a 'test' token at every execution

On the server side I need to inject a Fake implementation of my validator
```cs
if (Configuration.GetValue<string>("test") != null)
{               
    services.AddSingleton<ICaptchaValidator, FakeCaptchaValidator>();
}
```
```cs
public class FakeCaptchaValidator : ICaptchaValidator
    {
        public bool NextResult { get; set; } = true;
        public Task Check(string token)
        {
            if (!NextResult)
                throw new InvalidOperationException();
            return Task.CompletedTask;
        }
    }
```
- Because I send "-test true" when I execute my server for E2E test, the good implementation is injected.
- with NextResult in my FakeCaptchaValidator I can validate that my process fails when the captcha validation fails

## Conclusion
In this article we saw again two major advantages of Blazor : the code sharing between client and server and the js interoperability which enables you to use any existing javascript api.

## References
- <https://github.com/jbogard/MediatR/wiki/Behaviors>
- <https://www.google.com/recaptcha/intro/v3.html>
