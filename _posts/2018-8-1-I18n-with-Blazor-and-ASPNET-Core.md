---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor, I18N, ASPNET Core]
---
# Internationalizing a Blazor App with ASPNET Core as backend service

A common task for developers is to make their aplication translated into the users language. Most of the frameworks provide tools for enabling easily such a task and let the developer focus on other things like stupid requirement or buggy API. Blazor being a new framework there isn't such a thing. In this blog post I'll show you my way of doing it.

## Server side
The translation will be stored server side and the client will request for all the tranlations in its language. I do this for avoiding downloading all the translations in all the languages on the client side which could be heavy on a large app. My server side is an APSNET Core 2.1 app so I'll use the existing feature for managing those.

### Set-up
The buit-in translation system is easy to setup. In the ConfigureService method you add 

```cs
services.AddLocalization(options => options.ResourcesPath = "Resources");
```

The folder name will be used later.

And on the Configure method you add

```cs
  var supportedCultures = new[]
  {
      new CultureInfo("en"),
      new CultureInfo("fr"),
  };
  app.UseRequestLocalization(new RequestLocalizationOptions
  {
      DefaultRequestCulture = new RequestCulture("en"),
      // Formatting numbers, dates, etc.
      SupportedCultures = supportedCultures,
      // UI strings that we have localized.
      SupportedUICultures = supportedCultures
  });
```

Where you set all the languages that can be handled by your app. ASPNET Core will use the Http header "Accept-Language" for deciding which translations to use and if it's not supported it'll use the default one.

### Resource file
All the translated content is stored on resources files which will be embeded on the server assemblies once deployed. You need to create a Resources folder and inside add a resource file called "Client.fr.resx" : 
 - translations are grouped by class to translate so here we'll use a dummy class called "Client" for all the translations for the client side
 - "fr" is the 2 letter code of the culture I want to translate I coulduse more specific culture like "fr-CA".
 
Visual Studio gives a table editor for this kind of files but the xml content is pretty straighforward and you can find my test data [here](https://github.com/RemiBou/Toss.Blazor/blob/master/Toss/Toss.Server/Resources/Client.fr.resx).

### API
Now I need an API for sending all the translatiosn in a given language. Here is my API

```cs
[ApiController, Route("api/[controller]/")]
    public class I18nController : ControllerBase
    {
        private IStringLocalizer<Client> stringLocalizer;

        public I18nController(IStringLocalizer<Client> stringLocalizer)
        {
            this.stringLocalizer = stringLocalizer;
        }

        [HttpGet]
        public ActionResult GetClientTranslations()
        {
            var res = new Dictionary<string, string>();
            return Ok(stringLocalizer.GetAllStrings().ToDictionary(s => s.Name, s => s.Value));
        }
    }
```

- IStringLocalizer is an ASPNET Core interface for localizing content
- Client in an empty class I created for grouping all the client translations
- There is no language passed because the framework will get it from the HTTP header "Accept-Language" and initialize the IStringLocalizer with the good translations. You can also setup ASPNET Core so it takes the language from url parameters, cookies etc...

## Client Side

### Service

I'll create a service that'll load the translations in the good language. Here is my service

```cs
public class RemoteI18nService : II18nService
    {
        private readonly IHttpApiClientRequestBuilderFactory httpApiClientRequestBuilderFactory;
        private string _lg;
        private Lazy<Task< Dictionary<string, string>>> translations;

        public RemoteI18nService(IHttpApiClientRequestBuilderFactory httpApiClientRequestBuilderFactory)
        {
            this.httpApiClientRequestBuilderFactory = httpApiClientRequestBuilderFactory;
            translations = new Lazy<Task<Dictionary<string, string>>>(() => FetchTranslations(null));
        }

        private async Task<Dictionary<string, string>> FetchTranslations(string lg)
        {
            var client = httpApiClientRequestBuilderFactory.Create("/api/i18n");
            if (lg != null)
                client.SetHeader("accept-language", _lg);
            Dictionary<string, string> res = null;
            await client.OnOK<Dictionary<string, string>>(r => res = r).Get();
            return res;
        }

        public async Task<string> Get(string key)
        {
            return !(await translations.Value).TryGetValue(key, out string value) ? key : value;
        }
       
        public void Init(string lg)
        {
            translations = new Lazy<Task<Dictionary<string, string>>>(() => FetchTranslations(lg));
        }
    }
```

 - The IHttpApiClientRequestBuilderFactory is just a wrapper around HttpClient I created for handling the different response code easily
 - I use a Lazy loading so I'm sure the translations will be requested only once. And my service will be injected as Singleton (I try to avoid those on server side but on client side it's really useful). Here is the DI setting on Program.cs
 
```cs
 serviceProvider = new BrowserServiceProvider(configure =>
            {
                ...
                configure.Add(new ServiceDescriptor(
                   typeof(II18nService),
                   typeof(RemoteI18nService),
                   ServiceLifetime.Singleton));
            });
```

### Component
Now I need a component that'll display the good translation for a given key. Here is mine

```cs
@inject II18nService i18n;


@displayValue


@functions{
    [Parameter]
    string key { get; set; }

    public string displayValue { get; set; }
    protected override async Task OnInitAsync()
    {
        await i18n.Get(key).ContinueWith(t =>
        {
            displayValue = t.Result;
            this.StateHasChanged();
        });
    }

}
```

 - I add no other choice than to use ContinueWith/StateHasChanged because the string wouldn't get updated otherwise.
 - I couldn't use directly the i18nService in the views because view render is not async (issue [1240](https://github.com/aspnet/Blazor/issues/1240) and my Get() method is
 - I think this technique might be wrong as it would create a lot of very small component on a large app. But it's the only way I found so far.
 
### Integration
 For displaying a translation I do it like that :
 
 ```cs
 <Trad key="Welcome" />
 ```
 
## Conclusion
I chose to use the same translation storage mechanism (resource file) for both back and front end so it'll make those file management easier and we'll be able to reuse some translation across both part. The developer experience is not perfect and there could be an impact on performance, but still it's working. The biggest challenge here was finding a good async method for calling the Get method as in the current version of Blazor (0.5.1) call to ".Result" or ".Wait" on a Task result on browser hanging.
