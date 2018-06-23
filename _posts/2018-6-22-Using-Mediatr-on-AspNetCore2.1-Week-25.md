## CQRS
CQRS means Command and Query Relationship Seggregation. This is a design pattern or a development practice on which you split your domain in 2 parts : the write and the read model.

### The write model (commands)
This is where all the update/insert/delete occurs. A command has a durable impact on the system, it changes the system state. Most of the time this part is the most complex as you'll have most the business logic and the classes are the most complex.
When you send a command, you don't expect anything back. Why ? Because the read model that was used before (for showing the form) forbid the user to emit unsuccesfull command. 

In a web application / rpc via http api / rest api the command are launched by a POST, even though it deletes row in the database. Because in CQRS command are not typed like in the real world : a user don't want to delete a booking records, he wants to cancel a booking (which means deleting a record but also sending mail, issuing refund, alerting mangement ...)

### The read model (queries)
This is where all the select occurs. A query is a way to display a part of the state of the system to the user. If you just emit queries the system state won't change. This part is where all the performance work is done as it's the most used part of the system : how many hotel do you look at before booking one stay ? 

In a web application / rpc via http api / rest api the command are launched by a GET. The only exception being for technical reason (request too big). It's a GET so the user is able to get this query result whenever he wants to.

### Why split this that way :
- Decoupling : by splitting your domain in two part, where there was one, you can make them much more independant. The read model will always be coupled to the write model, at least when you'll update it, but the write model won't care how you read the data it'll just try to be as coherent as possible and follow the business expert view of it (CQRS is tighly linked to DDD). The raising of domain event will make the write model totally ignore about the read model.
- Domain following : it's very important to have a code base as close as possible to what the domain expert think. Domain expert don't think database rows (CRUD), they think actions (command), end user screens (queries) and event/trigger.
- Performance : if you store both domain in a data store optimised for it, you'll be able to get better performance. For instance you store the write model on a RDMS so you are sure that your system state is coherent and you can store the read model on a NoSQL database (like Redis) because you don't want to execute 150 joins when you want to display some data to the user. 

## Mediatr
If you want to implement CQRS you'll have to do a lot of boilerplate code for wiring messages and their handling or you can use a package that does all this work for you.

Mediart is a .net open source projet ([GitHub](https://github.com/jbogard/MediatR),[Nuget](https://www.nuget.org/packages/MediatR/)) created by [Jimmy Bogard](https://twitter.com/jbogard). It's an implementation of the Mediator pattern and this pattern was created for decoupling message from handling.
## Usage
### Install
Mediatr is available on nuget for .net standard 2.0 project (it's also available for .net framework projects). You just enter this command on the package manager console :

> Install-Package MediatR

### Wiring with Ioc container
For linking messages and handling, MediatR needs an IoC container. If you think the one included in Asp.Net core 2.1 is enough you have to install the package for this container :

> Install-Package MediatR.Extensions.Microsoft.DependencyInjection

(this package as a dependance to the first one so only this one is enough).
And you configure MediatR like this in your Startup.ConfigureService

```cs
services.AddMediatR(typeof(Startup));
```

I use the type Startup so MediatR will scan all my aspnet core project for implementation of the required interface.

You can view my code here : [https://github.com/RemiBou/Toss.Blazor/blob/master/Toss/Toss.Server/Startup.cs]. I also add a reference to the shared assembly between my client (blazor app) and server where I got the definitions of my commands and queries.

### Issuing Command / Query
Once everything is bootstrapped you can create your first message. I created one for login-in the user. This kind of cases are weird because I don't update the database but in my mind it's still the global state of the system, in Mediatr there is not much difference between a command and a query anyway. Here is my command, quite simple

```cs
public class LoginCommand : IRequest<LoginCommandResult>
    {
        [Required]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
```
- It's a C# object which implements a generic "marker interface" IRequest (there is no method to implement) 
- I have DataAnnotations validation attribute for validating this message
- The generic argument of IRequest is the type of the result returned by the handler. Here it's a command with a result, simply because the user might enter bad login or password. I could manage to have no result as it's a command but it's easier to do that way.

For handling this command you nee two things. First the mediator instance : IMediatr (the implementation was declared before on the Startup) :

```C#
private readonly IMediator _mediator;
public AccountController(IMediator mediator)
{
    _mediator = mediator;
}
```
The handling is defined like this
```C#
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginCommandResult>
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger _logger;

    public LoginCommandHandler(SignInManager<ApplicationUser> signInManager, ILogger logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    public async Task<LoginCommandResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var result = await _signInManager.PasswordSignInAsync(request.UserName, request.Password, request.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in.");
            return new LoginCommandResult() { IsSuccess = true };
        }
        if (result.RequiresTwoFactor)
        {
            return new LoginCommandResult() { Need2FA = true };
        }
        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out.");
            return new LoginCommandResult() { IsLockout = true };
        }
        else
        {
            return new LoginCommandResult() { IsSuccess = false };
        }
    }
}
```
This code is pretty  simple. I just needed to implement "IRequestHandler<LoginCommand, LoginCommandResult>".

Then you call it that way
```C#
[HttpPost]
[AllowAnonymous]
public async Task<IActionResult> Login(LoginCommand command)
{
    var result = await _mediator.Send(command);
    if (!result.IsSuccess)
    {
        if (result.IsLockout)
            return Redirect("/lockout");
        ModelState.AddModelError("UserName", "Invalid login attempt.");
        return BadRequest(ModelState);

    }
    //if (result.Need2FA)
    //{
    //    return RedirectToAction("/loginWith2fa");
    //}
    return Ok();
}
```
- First line calls the mediator and get the result asynchronously (you could have IO under this so it's better to do everything async)
- Then I parse the result, most of this code is from the original security template for asp.net. I commented the 2FA part as I didn't implement it yet.
- I reuse the ModelState for sending the error as it's the format waited for the client (rpc via http) in the event of a bad request (400)
This is the most complicated code I have on my controller action, most of the time it's just like this :
```C#
var res = await mediator.Send(command);
if (res.IsSucess)
    return new OkResult();
return new BadRequestObjectResult(res.Errors);
```
So why have a Controller at all ? Mainly because it has a few things to do : routing / http verbs / redirection and in some case like the login part, something more complex.
### Events
I am using Azure Table for data storage. This service doesn't provide indexing out of the box, so you have to do it yourself. With event I can safely decouple my write model (inserting new element) and my read model (querying element based on some criteria). An event is pretty much like a command but implements INotification and there is no result here as the caller doesn't care about what happens next :
```C#
public class TossPosted : INotification {
    public string TossId{get;set;}
}
```
- A toss is a message on my application (like a post or something)
- I just put the tossId , I could put the whole Toss content but I choose to do like this

Then for handling the event and create the index you implement INotificationHandler like this

```C#
public class TossTagIndexHandler : INotificationHandler<TossPosted> {
    ///missing code : dependency injection, azure table init ...
    public Task Handle(TossPosted notification, CancellationToken cancellationToken) {       
        await mainTable.CreateIfNotExistsAsync();
        
        var toss = await _mediator.Send(new TossContentQuery(notification.TossId);
        
        var tasks = HashTagIndex
            .CreateHashTagIndexes(toss)//this create an entry for each hashtag entered into a toss
            .Select(h => mainTable.ExecuteAsync(TableOperation.Insert(h))
            .ToList();
        await Task.WhenAll(tasks);
    }
}
```
- with this I can add as many indexing strategy as I wich. I will have to create some batch processing when deploying it though.
- I can use an other implementation for handling this on an other server or cloud service
- I also could decouple it more and the event handling would just push new CreateTossTagIndexCommand, but that would be too much code for no gain.
### Validation
You didn't s*ee any command validation here, it's normal I use the new ApiController from aspnet core 2.1 who handles returning a 400 bad request with the model state if one of my attribute is not respected. And that's enough for me now.
## Conclusion
Mediatr really helped to decouple all my command / query / event handling. It also helped to keep my controller small as it's, in my opnion, one of the biggest problem in an mvc application.
