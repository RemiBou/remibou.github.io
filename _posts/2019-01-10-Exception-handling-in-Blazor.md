---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor]
---
# Handling Exception in Blazor
Like in every part of your system, on the front-end part you should take care of exceptions. Like its name says : it should not happen. But we are not perfect and they happen because of a lot of reasons. For now exceptions that happens in Blazor are sent to the browser console. But we might need more than that : display it on the web page or even send it to our backend for alerting the team that something went wrong.

## How Blazor does this now
The exception logging is done on the ComponentBase class [here](https://github.com/aspnet/AspNetCore/blob/master/src/Components/src/Microsoft.AspNetCore.Components/ComponentBase.cs). The method HandleException line 209 does the work :

```cs
 private static void HandleException(Exception ex)
{
    if (ex is AggregateException && ex.InnerException != null)
    {
        ex = ex.InnerException; // It's more useful
    }

    // TODO: Need better global exception handling
    Console.Error.WriteLine($"[{ex.GetType().FullName}] {ex.Message}\n{ex.StackTrace}");
}
```
Then the C# Console.Error.WriteLine is changed in js "console.error" by the mono runtime, just like Console.WriteLine is change in js "console.log". Don't ask me where it happens, the mono source code is quite hard to read for me.
We can see here there isn't many extension point. There is a way for overriding static method at runtime ([here](https://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method)) but it seems risky. 

The only entry point I can think of is Console.Error : there is a Console.SetError(textWriter) that I could use for sending my own implementation.

## Implementing TextWriter

The implementation of TextWriter is quite easy here it is :

```cs
   public class ExceptionNotificationService : TextWriter, IExceptionNotificationService
    {
        private TextWriter _decorated;

        public override Encoding Encoding => Encoding.UTF8;

        public ExceptionNotificationService()
        {
            _decorated = Console.Error;
            Console.SetError(this);
        }
        //THis is the method called by Blazor
        public override void WriteLine(string value)
        {
            //do something with the error

            _decorated.WriteLine(value);
        }
    }
```

- I use the decorator pattern, the exception message will still be send to console.error.
- TextWriter is not an interface but an abstract class for which most of the methods are implemented : only "Encoding" needs to be implemented. So here I override the method called by Blazor "WriteLine(string)". This code won't work if the Blazor team changes something in their exception handling.

Then I inject my service as a singleton like this in StartUp.cs :

```cs
services.Add(new ServiceDescriptor(
    typeof(IExceptionNotificationService),
    typeof(ExceptionNotificationService),
    ServiceLifetime.Singleton));
```

## What I do with error
Like the interface seems to suggest, I'll be using event for propagating exception, so I change a bit my class like this :

```cs
 public class ExceptionNotificationService : TextWriter, IExceptionNotificationService
{
    private TextWriter _decorated;

    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>
    /// Raised is an exception occurs. The exception message will be send to the listeners
    /// </summary>
    public event EventHandler<string> OnException;

    public ExceptionNotificationService()
    {
        _decorated = Console.Error;
        Console.SetError(this);
    }
    //THis is the method called by Blazor
    public override void WriteLine(string value)
    {
        //notify the listenners
        OnException?.Invoke(this,value);

        _decorated.WriteLine(value);
    }
}
```

- When an exception occurs every class that registered a listenner to the event OnException will get notified.

Then for registering I did this in my Layout :

```cs
protected override async Task OnInitAsync()
{
    this.exceptionNotificationService.OnException += HandleExceptions;
    base.OnInit();
}
private void HandleExceptions(object sender, string s)
{
    JsInterop.Toastr("error", s).ContinueWith((a) => { });
}
public void Dispose()
{
    this.exceptionNotificationService.OnException -= HandleExceptions;
}

- The JsInterop.Toastr method just display a toastr.
- Don't forget to unsubcribe or the instance of your component will stay around for ever
- I couldn't find how to make this code async/await. So for async calls I had to add an empty ContinueWith as Wait() does not work on mono.

## Conclusion
It took me a while to find this solution but I'm glad I found one (it was on my blog post to-write list). This solution has many drawbacks : it depends on implementation detail, it does handle loop and async well and it just send a text instead of the full exception object. But it works and helped me understand a bit better how Blazor and mono wasm work.