# Setup an Azure DevOps CI pipeline with E2E tests against a ASPNET Core server
In my project Toss, I have classic Unit / Integration test but also end-to-end tests with Selenium WebDriver. I need those test for 2 reasons :
- You always need them :) Because it's the last step of integration, and you can't think of all the things that could go wrong when you write your unit / integration tests
- Blazor is an experimental technology with new version every 1-2 months that could introduce breaking changes. I need a way to know that after an upgrade my app is still ok.

Most of the time E2E tests are run as part of the deployment process because you need to have the full system deployed : your web site but also your dependencies (database, other services...)

## Selenium Chrome Web Driver

Selenium Chrome Driver is a technology that enables communication between a software and a browser so we can run test just like a human would. In .NET you use the package [Selenium.Chrome.WebDriver](https://www.nuget.org/packages/Selenium.Chrome.WebDriver/). Your code will look like something like that  :

```cs
[Fact]
public void FullE2eTest()
{
    var driver = new ChromeDriver();
    driver.Manage().Window.FullScreen();
    driver.Navigate().GoToUrl("http://localhost:9898/");
    Assert.Equal("TOSS", driver.Title);
}
```

- I use xUnit for running this
- This just goes to the home page of my app and checks the page title

## Starting the ASPNET Core server

One of the challenge here is to run the ASPNET Core app with the test runner (xUnit) so the chrome instance manipulated by web driver will be able to run the tests. For doing this I got the solution from the [Blazor repo](https://github.com/aspnet/Blazor). ASPNET Core can run with it's own web server called Kestrel. We need to execute the BuildWebHost with the right argument in a background thread like this :

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

- We use a Fixture in xUnit which is something that'll stay around for the whole execution of the tests
- We need to get the website content root for delivering static content (like my Blazor app binaries), this is done by FindClosestDirectoryContaining
- I inject 2 fake dependencies, I'll talk about it later
- I just need to inject this Fixture into my test so I'll be able to get the server URL with

## Faking dependencies

## Azure DevOps integration
