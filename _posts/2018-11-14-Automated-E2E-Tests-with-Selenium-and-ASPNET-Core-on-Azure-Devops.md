# Setup an Azure DevOps CI pipeline with E2E tests against a ASPNET Core server
In my project Toss, I have classic Unit / Integration test but also end-to-end tests with Selenium WebDriver. I need those test for 2 reasons :
- You always need them :) Because it's the last step of integration, and you can't think of all the things that could go wrong when you write your unit / integration tests. Here you are 99% sure that the basic usage of your app is not broken by a build.
- Blazor is an experimental technology with new version every 1-2 months that could introduce breaking changes. I need a way to know that after an upgrade my app is still ok.

Most of the time E2E tests are run as part of the deployment process because you need to have the full system deployed : your web site but also your dependencies (database, file system, web server, other services...)

## Selenium Chrome Web Driver

Selenium Chrome Driver is a technology that enables communication between a software and a browser so we can run test just like a human would. In .NET you use the package [Selenium.Chrome.WebDriver](https://www.nuget.org/packages/Selenium.Chrome.WebDriver/). Your code will look like something like that  :

```cs
[Fact]
public void FullE2eTest()
{
    var driver = new ChromeDriver();
    driver.Navigate().GoToUrl("http://localhost:9898/");
    Assert.Equal("TOSS", driver.Title);
}
```

- I use xUnit for running this
- This just goes to the home page of my app and checks the page title

## Starting the ASPNET Core server

One of the challenge here is to run the ASPNET Core app within the test runner (xUnit) so the chrome instance manipulated by web driver will be able to run the tests. For doing this I got the solution from the [Blazor repo](https://github.com/aspnet/Blazor). ASPNET Core can run with it's own web server called Kestrel. We need to execute the BuildWebHost with the right argument in a background thread like this :

```cs
public class AspNetSiteServerFixture : IDisposable
    {
        public FakeEmailSender EmailSender { get; private set; }
        public Uri RootUri => _rootUriInitializer.Value;

        public IWebHost Host { get; set; }
  
        private readonly Lazy<Uri> _rootUriInitializer;

        public AspNetSiteServerFixture()
        {
            _rootUriInitializer = new Lazy<Uri>(() =>
                new Uri(StartAndGetRootUri()));
        }     

        private static string FindClosestDirectoryContaining(
            string filename,
            string startDirectory)
        {
            var dir = startDirectory;
            while (true)
            {
                if (File.Exists(Path.Combine(dir, filename)))
                {
                    return dir;
                }

                dir = Directory.GetParent(dir)?.FullName;
                if (string.IsNullOrEmpty(dir))
                {
                    throw new FileNotFoundException(
                        $"Could not locate a file called '{filename}' in " +
                        $"directory '{startDirectory}' or any parent directory.");
                }
            }
        }

        protected static void RunInBackgroundThread(Action action)
        {
            var isDone = new ManualResetEvent(false);

            new Thread(() =>
            {
                action();
                isDone.Set();
            }).Start();

            isDone.WaitOne();
        }

        protected string StartAndGetRootUri()
        {
            Host = CreateWebHost();
            RunInBackgroundThread(Host.Start);
            EmailSender = Host.Services.GetService(typeof(IEmailSender)) as FakeEmailSender;
            return Host.ServerFeatures
                .Get<IServerAddressesFeature>()
                .Addresses.Single();
        }

        public void Dispose()
        {
            // This can be null if creating the webhost throws, we don't want to throw here and hide
            // the original exception.
            Host?.StopAsync();
        }

        protected IWebHost CreateWebHost()
        {
            var solutionDir = FindClosestDirectoryContaining(
                          "Toss.sln",
                          Path.GetDirectoryName(typeof(Program).Assembly.Location));
            var sampleSitePath = Path.Combine(solutionDir, typeof(Toss.Server.Program).Assembly.GetName().Name);
          
            return Toss.Server.Program.BuildWebHost(new[]
            {
                "--urls", "http://127.0.0.1:0",
                "--contentroot", sampleSitePath,
                "--environment", "development",
                "--databaseName",CosmosDBFixture.DatabaseName,
                "--test","true"
            });
        }
    }
```

- It's a lot of code, I'm sorry but I can't find any
- We use a Fixture in xUnit which is something that'll stay around for the whole execution of the tests
- We need to get the website content root for delivering static content (like my Blazor app binaries), this is done by FindClosestDirectoryContaining
- I send "--test" so my server will run with fake dependencies for the thing I can't setup (like email platform or stripe)
- I just need to inject this Fixture into my test so I'll be able to get the server URL with the RootUri

## Faking dependencies

Now I need to reed the test parameter on ASPNET Core side so I don't inject real implementation of my dependencies. I do it like that :

```cs
public void ConfigureServices(IServiceCollection services)
{
    // Add application services.
    if (Configuration.GetValue<string>("test") == null)
    {
        services.AddTransient<IRandom, RandomTrue>();
        services.AddTransient<IEmailSender, EmailSender>();
        services.AddSingleton<IStripeClient, StripeClient>(s => new StripeClient(Configuration.GetValue<string>("StripeSecretKey")));
    }
    else
    {
        services.AddSingleton<IRandom, RandomFake>();
        //We had it as singleton so we can get the content later during the asset phase
        services.AddSingleton<IEmailSender, FakeEmailSender>();
        services.AddSingleton<IStripeClient, FakeStripeClient>();
    }
}
```

- I need to inject Random so my test result is predictible
- With this code "EmailSender = Host.Services.GetService(typeof(IEmailSender)) as FakeEmailSender;", I can get the content of the email the system was suposed to send (like activation link).

## Azure DevOps integration

Running these test on Azure DevOps shouldn't be harder than on the local machine but there is a few things that needs to be done.

FIrst we need to use the webdriver installed on the agent. The webdriver path is passed as an environment variable so we can read it like that

```cs
public class BrowserFixture : IDisposable
    {
        public IWebDriver Browser { get; }

        public ILogs Logs { get; }

        public ITestOutputHelper Output { get; set; }

        public BrowserFixture()
        {
            var opts = new ChromeOptions();
            var binaryLocation = Environment.GetEnvironmentVariable("ChromeWebDriver");
            if (string.IsNullOrEmpty(binaryLocation))
            {
                binaryLocation = ".";
            }

            var driver = new ChromeDriver(binaryLocation,opts,TimeSpan.FromMinutes(3));
            Browser = driver;
        }

        public void Dispose()
        {
            Browser.Dispose();
        }
    }
```

- This fixture will be injected on all my E2E tests so it'll be run only once
- the third parameter of the webDriver constructor gives the default command timeout. It seems that the VM agent are a bit slow so you'll have to be patient (but it's free so ...)

A few more things I had to fix :
- The modal foreground takes a bit longer to disapear so I have to add a wait like this 
```cs
_webDriveWaitDefault.Until(b => !b.FindElements(By.CssSelector(".modal-backdrop")).Any());
```
- The application appsettings.json must be filled with empty value for secret because the secrets are stored on the dev computer
- I had to force the web driver to be displayed on fullscreen because some button were not visible by default
```cs
Browser.Manage().Window.FullScreen();
```

## Conclusion

It was not really hard to setup but the VM agent are really slow, so each test takes around 10min. Now I have a working CI pipeline, you can see it here <https://dev.azure.com/remibou/toss/_build?definitionId=1>.

## Reference
- <https://github.com/aspnet/Blazor>
- <https://docs.microsoft.com/en-us/azure/devops/pipelines/test/continuous-test-selenium?view=vsts>
