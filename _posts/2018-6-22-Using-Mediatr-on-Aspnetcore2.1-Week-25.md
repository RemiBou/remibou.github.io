## CQRS
CQRS means Command and Query Relationship Seggregation. This is a design pattern or a development practice on which you split your domain in 2 parts : the write and the read model.

### The write model (commands)
This is where all the update/insert/delete occurs. A command has a durable impact on the system, it changes the system state. Most of the time this part is the most complex as you'll have most the business logic and the classes are the most complex.
When you send a command, you don't expect anything back. Why ? Because the read model that was used before (for showing the form) forbid the user to emit unsuccesfull command. 
In a web application / rpc via http api / rest api the command are launched by a POST, even though it deletes row in the database. Because in CQRS command are not typed like in the real world : a user don't want to delete a booking records, he wants to cancel a booking (which means deleting a record but also sending mail, issuing refund, alerting mangement ...)

### the read model (queries)
This is where all the select occurs. A query is a way to display a part of the state of the system to the user. If you just emit queries the system state won't change. This part is where all the performance work is done, it's the most used part of the system : how many hotel do you look at before booking one stay ? 
In a web application / rpc via http api / rest api the command are launched by a GET. The only exception being for technical reason (request too big). It's a GET so the user is able to get this query result whenever he wants to.

### Why split this that way :
- Decoupling : by splitting your domain in two part where there was one, you can make them much more independant. The read model will always be coupled to the write model, at least when you'll update it, but the write model won't care how you read the data it'll just try to be as coherent as possible and follow the business expert view of it (CQRS is tighly linked to DDD in my point of view).
- Domain following : it's very important to have a code base as close as possible to what the domain expert think. Domain expert don't think database rows (CRUD), they think actions (command) and end user screens (queries).
- Performance : if you store both domain in a data store optimised for it, you'll be able to get better performance. For instance you store the write model on a RDMS so you are sure that your system state is coherent and you can store the read model on a NoSQL database (like Redis) because you don't want to execute 150 joins when you want to display some data to the user. The read model is updated everytime something happens on the write model via event so the write model completly ignores what it is supposed to update.

### Difficulties
When you want to implement CQRS you have a few difficulties, in C# at least :
- Implementing event aggregation, for updating your read model and chaining commands if needed. The current event implementation on C# is just usefull for client side GUI and cannot be used on a stateless app like a website
- Decoupling command and query from their handling the boilerplate for linking the command/query to its good handler can be cumbersome. Why decoupling the message (command/query) from the handling ? First they can be defined in different assemblies (you can share the class definition of the queries with your client, not the implementation), second you won't have to inject your services into your message class and third your code will respect the SRP. 
This is why it's better to use a tool like Mediatr for implementing CQRS.

## Mediatr
Mediart is a .net open source projet ((GitHub)[https://github.com/jbogard/MediatR],(Nuget)[https://www.nuget.org/packages/MediatR/]) created by (Jimmy Bogard)[https://twitter.com/jbogard]. It's an implementaiton of the Mediator pattern and this pattern was created for decoupling message from handling.
## Usage
### Install
Mediatr is available on nuget for .net standard 2.0 project. You just enter this command on the package manager console
```
Install-Package MediatR
```
### Wiring with Ioc container
For linking messages and handling, MediatR neds an IoC container. If like me you think the one included in Asp.Net core 2.1 is quite enough you have to install the package for this container
```
Install-Package MediatR.Extensions.Microsoft.DependencyInjection
```
(this package as a dependance to the first one so only this one is enough).
And you configure MediatR like this in your Startup.ConfigureService
```C#
services.AddMediatR(typeof(Startup));
```
I use the type Startup so MediatR will scan all my aspnet core project for implementation of the required interface.
You can view my code here : [https://github.com/RemiBou/Toss.Blazor/blob/master/Toss/Toss.Server/Startup.cs]. I also add a reference to the shared assembly between my client and server where I got the definitions of my commands and queries.

### Issuing Command / Query
Once everything is bootstrapped you can create your first message so I created one for login in the user. This kind of cases are weird because I don't update the database but in my mind it's still the global state of the system. So here is my command, quite simple

```C#
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
- it's a C# object which implements a generic "marker interface" IRequest (there is no method to implement) 
- I have DataAnnotations validation attribute for validating this message
- The generic argument of IRequest is the type of the result returned by the handler. Here it's a command with a result, simply because the user might enter bad login or password. I coul
### Events
### Validation
## Conclusion
