---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Tests, MediatR, ASPNET Core, Cosmos DB]
---

# Moving from unit test to integration/system test 
*This post is not directly about Blazor but I wanted to blog about this technical change.*

On my Toss project I decided to move from a Unit Test approach to an Integration test approach. In this blog post I'll try to explain the reasons and the technique employed.

## How I build my unit test

I build my unit test this way : a method (unit) has input (parameters and dependencies) and output (return, dependencies and exceptions). 

A unit test will "arrange" the input :
- the parameters are forced
- the dependencies are setup via a mocking framework (Setup() in Moq)
And "assert" the output :
- the expected return and exceptions are checked
- the dependencies are validated via the mocking framework (Verify() in Moq)

In theory this is perfect : 
- only production code is executed at test assert, I don't depend on other systems.
- if one test fails, the reason will be easy to find.
- every piece of code will be writen following the red/green process (create test and add code until tests passes).

## Problems with unit tests

This approach has drawbacks that lead me to getting rid of it :
- Because of the mandatory mocking/faking of dependencies, you are not testing an interface but an implementation. And a method is the lower block of implementation. Every time I'll do a small change in my implementation (for instance I change a loop on "SaveOne" for a call to "SaveAll") I will need to change my tests.
- Setting up everything is a lot of code, look at [this file](https://github.com/RemiBou/Toss.Blazor/blob/46c2440b4a57e224464d5f6f61cd8b302f54aa47/Toss.E2ETest/Infrastructure/MockHelpers.cs) I had to create to mock the ASPNET Core Identity dependencies. If I have the simple formula "1 LoC  = X bugs", we can say that I will spend more time debugging my tests (and that's what happened) than my actual code !
- Because you are not testing everything altogether you can have problem at runtime :  you didn't setup DI the right way, because your class doesn't implement the good interface, your configuration is not set ... 

## Technical solution
The solution is system test, we could call it integration test but for some of them there might be no dependency involved. Here I want to test my system as a whole.

### Use the ASPNET Core DI setup

Here is my class setting up this :
```cs
public class TestFixture
{
    public const string DataBaseName = "Tests";
    public const string UserName = "username";
    private static ServiceProvider _provider;
    //only mock we need :)
    private static Mock<IHttpContextAccessor> _httpContextAccessor;

    public static ClaimsPrincipal ClaimPrincipal { get; set; }

    static TestFixture()
    {

        var dict = new Dictionary<string, string>
        {
             { "GoogleClientId", ""},
             { "GoogleClientSecret", ""},
             { "MailJetApiKey", ""},
             { "MailJetApiSecret", ""},
             { "MailJetSender", ""},
             { "CosmosDBEndpoint", "https://localhost:8081"},
             { "CosmosDBKey", "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="},
             { "StripeSecretKey", ""},
             {"test","true" },
             {"dataBaseName",DataBaseName }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
        var startup = new Startup(config);
        var services = new ServiceCollection();
        startup.ConfigureServices(services);
        _httpContextAccessor = new Mock<IHttpContextAccessor>();

        services.AddSingleton(_httpContextAccessor.Object);
        services.AddScoped(typeof(ILoggerFactory), typeof(LoggerFactory));
        services.AddScoped(typeof(ILogger<>), typeof(Logger<>));

        _provider = services.BuildServiceProvider();

    }

    public async static Task CreateTestUser()
    {
        var userManager =  _provider.GetService<UserManager<ApplicationUser>>();
        ApplicationUser user = new ApplicationUser()
        {
            UserName = UserName,
            Email = "test@yopmail.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user);
        ClaimPrincipal = new ClaimsPrincipal(
                  new ClaimsIdentity(new Claim[]
                     {
                                new Claim(ClaimTypes.Name, UserName)
                     },
                  "Basic"));
        (ClaimPrincipal.Identity as ClaimsIdentity).AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
        _httpContextAccessor
          .SetupGet(h => h.HttpContext)
          .Returns(() =>
          new DefaultHttpContext()
          {
              User = ClaimPrincipal

          });
    }

    public static void SetControllerContext(Controller controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContextAccessor.Object.HttpContext
        };
    }

    public static void SetControllerContext(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext  = _httpContextAccessor.Object.HttpContext
        };
    }

    public static T GetInstance<T>()
    {
        T result = _provider.GetRequiredService<T>();
        ControllerBase controllerBase = result as ControllerBase;
        if (controllerBase != null)
        {
            SetControllerContext(controllerBase);
        }
        Controller controller = result as Controller;
        if (controller != null)
        {
            SetControllerContext(controller);
        }
        return result;

    }
}
```

- I have to mock the HttpContextAccessor as there is no Http Query and I need it for knowing who is the connected user
- I pass "test" "true" to the config so I can setup my fake/mock in Configure()
- I had to force the logger DI setup, I guess it's set by something in ConfigureService

### Choosing the tested layer
I chose to test at the mediator layer (from MediatR), my controller layer is very thin so I prefer to test my app here.

My test setup are really simple, they are basically like this :

```cs
 var mediator = TestFixture.GetInstance<IMediator>();
 
 mediator.Send(new MyCommand());
 
 var res = mediator.Send(new MyQuery());
 
 Assert.Single(res);

```
- This test will uses both the command and query so with this 4 LoC I test a lot of code : DI setup, interface declaration, interface implementation ...

### External dependencies

I still need to mock some dependencies that I don't manage : Stripe or MailJet or even Random. 
Here is how I setup the fake
```cs
// Add application services.
if (Configuration.GetValue<string>("test") == null)
{
    services.AddTransient<IEmailSender, EmailSender>();
    services.AddSingleton<IStripeClient, StripeClient>(s => new StripeClient(Configuration.GetValue<string>("StripeSecretKey")));
}
else
{
    //We had it as singleton so we can get the content later during the asset phase
    services.AddSingleton<IEmailSender, FakeEmailSender>();
    services.AddSingleton<IStripeClient, FakeStripeClient>();
}
```
- The fake code are very simple (you can find it in my Toss repo) they just record the received parameters and have a static property for giving the next expected result

### Internal dependencies

I call internal dependencies, dependencies that I manage entirely like CosmosDB. CosmosDB doesn't support transaction with multiple client request like SQL Server(you have to create a server side sp for using transactions) so I have to clean up the database after each tests. Here is my base class for doing this :

```cs
public class BaseCosmosTest : IAsyncLifetime
{
    public BaseCosmosTest()
    {
    }

    public async Task InitializeAsync()
    {
    }

    public async Task DisposeAsync()
    {
        var _client = TestFixture.GetInstance<DocumentClient>();
        var collections = _client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(TestFixture.DataBaseName)).ToList();
        foreach (var item in collections)
        {
            var docs = _client.CreateDocumentQuery(item.SelfLink);
            foreach (var doc in docs)
            {
                await _client.DeleteDocumentAsync(doc.SelfLink);
            }
        }
    }
}
```
- I only remove the document not the collections, so my test will run faster
- IAsyncLifetime is here for having an async Dispose method
- I cannot clean the DB before the test as the DB will not be created until the Startup.Configure method is called
- I need to force the test to run non parallel (don't know if this term is correct in english but you get my point) as they all use the same DB/Collections here is the xunit.runner.json needed (you need to Copy it in output directory) :

```json
{  
  "parallelizeTestCollections": false
}
```

## New problems

There is of course drawbacks in this way of testing :
- A test can fail for many reason, so in the long term debugging failing test my be more difficult than with a unit test approach.
- You have to be able to clean all the dependencies between each test, if not they will fall in the external category. This mean writing "test only" code.
- The test will take longer to run.
- I don't test as much as I want : route, controller ... I could do only E2E test but they are too much pain to create, so I only have a few of them.

## Reference
- <http://david.heinemeierhansson.com/2014/tdd-is-dead-long-live-testing.html>
- <https://lostechies.com/jimmybogard/2015/02/19/reliable-database-tests-with-respawn/>
