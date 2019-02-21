# How to configure a Blazor application
Most of the applications we build need some kind of configuration : connection string, production or not, api key ... Client side app are not different, although what you'll store in the configuration must be less sensible as the client will have access to it. In this blog post I'll explain how I setup my Blazor project for accessing the different configuration needed in the different environment.

## How to define environement
Environement is the thing that will decide which configuration to use : development , test, production ... We cannot use build configuration (debug, release) as those two things are different : you can decide to run your application build with debug with the production configuration because you have a bug on production, or you can decide to deploy your release build on your test environement because you need to do one last check before releasing. 

In Blazor we don't have anything that looks like an environment (we have limited access to the client computer) or a executable argument that would set the configuration. The only thing that looks like it is URI : a Blazor app is launched by a call to a URI just like a program is launched by a command line. A URI is made of multiple part : 
- The protocol
- The domain name
- The path
- The query string
My solution will use the 2nd and the last part :
- An environement will be tied to a domain name ("myproject.com" will be the production and "localhost" will be the development environment)
- This environment can be overriden by a query string parameter so we can switch environment without rebuilding our app.

The task here is to do 2 things :
- Decide which environement we are working on
- Load the configuration based on this environment and make it available for the rest of the app.

## Find the environment on StartUp

The first step is to find the environment given a Uri, here is the class I created for this

```cs
/// <summary>
/// This class is used for picking the environment given a Uri
/// </summary>
public class EnvironmentChooser
{
    private const string QueryStringKey = "Environment";
    private string defaultEnvironment;
    private Dictionary<string, Tuple<string, bool>> _hostMapping = new Dictionary<string, Tuple<string,bool>>();

    /// <summary>
    /// Build a chooser
    /// </summary>
    /// <param name="defaultEnvironment">If no environment is found on the domain name or query then this will be returned</param>
    public EnvironmentChooser(string defaultEnvironment)
    {
        if (string.IsNullOrWhiteSpace(defaultEnvironment))
        {
            throw new ArgumentException("message", nameof(defaultEnvironment));
        }

        this.defaultEnvironment = defaultEnvironment;
    }

    public string DefaultEnvironment => defaultEnvironment;

    /// <summary>
    /// Add a new binding between a hostname and an environment
    /// </summary>
    /// <param name="hostName">The hostname that must fully match the uri</param>
    /// <param name="env">The environement that'll be returned</param>
    /// <param name="queryCanOverride">If false, we can't override the environement with a "Environment" in the GET parameters</param>
    /// <returns></returns>
    public EnvironmentChooser Add(string hostName, string env, bool queryCanOverride = false)
    {
        this._hostMapping.Add(hostName, new Tuple<string,bool>(env, queryCanOverride));
        
        return this;
    }

    /// <summary>
    /// Get the current environment givent the uri
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public string GetCurrent(Uri url)
    {
        var parsedQueryString = HttpUtility.ParseQueryString(url.Query);
        bool urlContainsEnvironment = parsedQueryString.AllKeys.Contains(QueryStringKey);
        if (_hostMapping.ContainsKey(url.Authority))
        {

            Tuple<string, bool> hostMapping = _hostMapping[url.Authority];
            if(hostMapping.Item2 && urlContainsEnvironment)
            {
                return parsedQueryString.GetValues(QueryStringKey).First();
            }
            return hostMapping.Item1;
        }
        if (urlContainsEnvironment)
        {

            return parsedQueryString.GetValues(QueryStringKey).First();
        }
        
        return DefaultEnvironment;
    }
}
```

- Sorry for the long code sample
- This class implements a simple algorithm and try to find the environent given a configuration and a uri
- I added the ability to ignore query parameters on production environment

## Init the configuration

Then I need to use EnvironmentChooser and inject the IConfiguration into my service collection, I do it like that :

```cs
public static void AddEnvironmentConfiguration<TResource>(
    this IServiceCollection serviceCollection,
    Func<EnvironmentChooser> environmentChooserFactory)
{
    serviceCollection.AddSingleton<IConfiguration>((s) =>
    {
        var environementChooser = environmentChooserFactory();
        var uri = new Uri(s.GetRequiredService<IUriHelper>().GetAbsoluteUri());
        System.Reflection.Assembly assembly = typeof(TResource).Assembly;
        string environment = environementChooser.GetCurrent(uri);
        var ressourceNames = new[]
        {
            assembly.GetName().Name + ".Configuration.appsettings.json",
            assembly.GetName().Name + ".Configuration.appsettings." + environment + ".json"
        };
        ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string>()
        {
            { "Environment", environment }
        });
        Console.WriteLine(string.Join(",", assembly.GetManifestResourceNames()));
        Console.WriteLine(string.Join(",", ressourceNames));
        foreach (var resource in ressourceNames)
        {

            if (assembly.GetManifestResourceNames().Contains(resource))
            {
                configurationBuilder.AddJsonFile(
                    new InMemoryFileProvider(assembly.GetManifestResourceStream(resource)), resource, false, false);
            }
        }
        return configurationBuilder.Build();
    });
}
```
- I use IUriHelper for getting the current Uri (it uses JSInterop)
- The InMemoryFileProvider is mostly taken from here (https://stackoverflow.com/a/52405277/277067), I just changed the class for accepting a stream as input
- I use convention for reading the ressources (folder Configuration then appsettings.json and appsettings.{Environment}.json)
- I also add the Environment name, it can be used later for debugging purpose 
- For this to work you need to add the packages Microsoft.Extensions.Configuration and Microsoft.Extensions.Configuration.Json to your project
- I could call JSInterop with the configuration so it'll be available to my js code as well.

Now I call it like that in Startup.ConfigureServices

```cs
services.AddEnvironmentConfiguration<Startup>(() => 
            new EnvironmentChooser("Development")
                .Add("localhost", "Development")
                .Add("tossproject.com", "Production", false));
```
- The Startup generic parameter is used for selecting the assembly that embedded the configuration files.

One more thing to do : I need to force the initialization of my singleton at startup. If I don't do that then the URI query parameter might not be here anymore if the user navigates and the IConfiguration is used for the first time. I chose to do this in Startup :

```cs
//In Startup.Configure
public void Configure(IComponentsApplicationBuilder app)
{
    //force config initialisation with current uri
    IConfiguration config = app.Services.GetService<IConfiguration>();
}
```

## Create the files
Now I need to create 3 files (none of them are mandatory as I check for their existence) :
- Configuration/appsettings.json
- Configuration/appsettings.Development.json
- Configuration/appsettings.Production.json

And set them as embedded ressources in my project csproj :

```xml
<ItemGroup>
    <EmbeddedResource Include="Configuration\appsettings.json" />
    <EmbeddedResource Include="Configuration\appsettings.*.json" />
</ItemGroup>
```

For instance here is my appsettings.Development.json

```json
{
  "title": "Welcome to Toss Development"
}
```

## Usage
For using it, you simply need to inject IConfiguration where you need it

```razor
@inject Microsoft.Extensions.Configuration.IConfiguration configuration
<h1>@configuration["title"]</h1>
```

## Conclusion
This project still shows one of the big advantage of Blazor : you can use most of the things done in .net in the browser. When Blazor will be shipped we won't have to wait 1-2 years for all the needed library to come up (i18n, configuration, serialization ...), they are already there since many years. 

You can find most of the code for this blog post on my project (https://github.com/RemiBou/Toss.Blazor).

## Reference
- https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-2.2#file-configuration-provider
- https://stackoverflow.com/a/52405277/277067