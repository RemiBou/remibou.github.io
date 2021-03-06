---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor, DataAnnotation]
---
# Client side validation with Blazor and System.DataAnnotation

Although I am not a big fan of client side validation (because you'll have to do the validation on server side anyway), there is always a time when using a client-side framework when you need to add some validation to your form and doing the simplest ones on the client-side can help you save some precious server CPU time. Also I don't like client-side validation because most of the time it doubles the work needed : you'll code the rules with your server-side technology (like ASPNET Core) and with your client-side technology (let's say Knockoutjs), this means that you have to keep both code in sync and you have to be able to do the same things in both side (and you can face difficulties like dealing with different implementations of regex).

With Blazor (and all the next generation of web assembly based frameworks) this problem disapears : everything that's available for your back-end service is available for your front-end app. Not everything in the case of Blazor, but everything that targets netstandard 2. And we are lucky enough, it's the case of System.ComponentModel.DataAnnotation which is open source and available here : <https://github.com/dotnet/corefx/tree/master/src/System.ComponentModel.Annotations/src>.

I could use an other validation API like Fluent Validation, but this one is simple, well integrated into the Microsoft toolbelt (EFCore, ASPNET Core MVC) and, in my opinion, is enough.

The following pieces are already available :
- Attribute for specifying constraints (you can also create your own attribute)
- Utility class for validating constraints against an instance
- Api for writing error message

But for the front-end part we still need to work on some stuff :
- Displaying validation error
- Prevent form submission when a validation error occurs

## Calling validation for an instance

The first step will be to define our model, here is the registration model from my project [Toss](https://github.com/RemiBou/Toss.Blazor) 

```cs
public class RegisterCommand : IRequest<CommandResult>
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

- The validation were already here because I already do server side validation <hich is called automaticaly on my ApiController

Then I create a service that will call the validation .Because we are in a GUI app (where object are long lived), I am using C# event for propagating validation errors. I made this choice for making the developer experience easier : he won't have to manage the validator creation, just add the label on the right place.

```cs
 /// <summary>
/// Validate a given instance
/// </summary>
/// <summary>
/// Validate a given instance
/// </summary>
public class ModelValidator : IModelValidator
{
    /// <summary>
    /// Raised is a validation error occurs
    /// </summary>
    public event EventHandler<ValidationErrorEventArgs> OnValidationDone;

    /// <summary>
    /// Validate the instance, if an error occurs returns false and raise the event
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instance"></param>
    /// <returns></returns>
    public bool Validate(object instance)
    {
        List<ValidationResult> res = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(instance, new ValidationContext(instance, null, null), res, true);

        OnValidationDone?.Invoke(this, new ValidationErrorEventArgs() { Errors = res, Instance = instance });
        return isValid;

    }
}
```

And now I inject my class as a singleton in Program.cs

```cs
configure.Add(new ServiceDescriptor(
    typeof(IModelValidator),
    typeof(ModelValidator),
    ServiceLifetime.Singleton));
```

## Prevent form submission
The first step is to prevent a form submission when the model is not valid. For this I'll create a custom component named "ValidatedForm" with child content like this :

```cs
@inject IModelValidator ModelValidator
<form onsubmit="@OnSubmitWithValidate">
    @ChildContent
</form>
@functions {
    [Parameter]
    private RenderFragment ChildContent { get; set; }

    [Parameter]
    private Func<UIEventArgs,Task> OnSubmit { get; set; }

    [Parameter]
    private object Model { get; set; }

    private async Task OnSubmitWithValidate(UIEventArgs eventArgs)
    {
        if (ModelValidator.Validate(Model))
            await OnSubmit(eventArgs);
    }
}
```

 - I can add much more parameters for setting different attributes of the form tag but for the sake of brievety I'll keep it that way
 
 And I call it like this :
 
 ```cs
 <ValidatedForm OnSubmit="CreateAccount" Model="registerCommand">
 //....
 </ValidatedForm>
 ```
 
 - registerCommand is the private field on my page holding the form data
 - CreateAccount will be called only if registerCommand is valid
 - Inside the form I can put anything I want including Blazor component
 
Now if I submit my form, nothing  gets submited BUT I don't tell anything to the user. So now I have to display some friendly error message.

## Display validation error message

For displaying validation error message I'll use the event emited by the validator in my component called "ClientValidationError" :

```cs
@implements IDisposable
@inject IModelValidator modelValidator;
@if (!string.IsNullOrEmpty(errorMessage))
{
    <span class="small form-text text-danger">@errorMessage</span>

}
@functions{
    [Parameter]
    string FieldName { get; set; }
    [Parameter]
    object Model { get; set; }

    protected string errorMessage;
    protected override void OnInit()
    {
        modelValidator.OnValidationDone += HandleError;
        base.OnInit();
    }

    protected void HandleError(object sender, ValidationErrorEventArgs args)
    {
        if (args.Instance != Model)
            return;
        errorMessage = null;
        var error = args.Errors?.FirstOrDefault(e => e.MemberNames.Any(m => m == FieldName));
        if (error == null)
            return;
        errorMessage = error.ErrorMessage;

    }
    public void Dispose()
    {
        modelValidator.OnValidationDone -= HandleError;
    }
}
```

 - It implements IDisposable for unregistering from the event, so the browser doesn't keep in memory all the ErrorMessage already displayed
 - We see here why I send the instance in the ValidationErrorEventArgs. The point here is to manage if I have multiple forms in the same page with the same field name.
 - I didn't integrate it with my I18n, I might do it later
 - FieldName will have to be manually entered by the developer, I don't support Expression for now
 - I can also create a ValidationSummary that would display all the errors for a given instance
 
And I add a validation error like this in the form you saw earlier

```cs
  <ClientValidationError FieldName="Email" Model="registerCommand" />
```

And it just works, the error message are displayed :) I didn't have to do 1 line of validation code because it already uses all the stuff done by Microsoft teams (and that's the main point of web assembly framework : reuse code across client and server).

## Conclusion

Wiring Blazor and DataAnnotation wasn't that hard and all the existing part of Blazor are already enough for doing all this. Maybe the developer experience could be better with Linq Expression, and we could try to add something for adding a css class to invalid textbox.

There is also one notable limitation : if you implement custom validation attribute that needs access to server ressources like a database, it will fail. But for these validation I don't think that managing it with DataAnnotation is really a good idea, sometimes a simple "if" is enough.

## Reference
- <https://blazor.net/docs/components/index.html#component-disposal-with-idisposable>
- <https://github.com/dotnet/corefx/tree/master/src/System.ComponentModel.Annotations/src>
- <https://blazor.net/docs/components/index.html#child-content>
