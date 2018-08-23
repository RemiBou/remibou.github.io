# Client side validation with Blazor and System.DataAnnotation

Although I am not a big fan of client side validation (because you'll have to do the validation on server side anyway), there is always a time when using a client-side framework when you need to add some validaiton to your form : required fields, regex ... I don't like client-side validation because most of the time it doubles the work needed : you'll code a validaiton with your server-side technology (like ASPNET Core) and with your client-side technology (let's say Knockoutjs), this mean that you have to keep both code in sync and you have to be able to do the same things in both side.

With Blazor (and all the next generation of web assembly based frameworks) this problem disapears : everything that's available for your back-end service is availablefor your front-end app. Not everything in the case of Blazor, but everything that targets netstandard 2. And we are lucky enough, it's the case of System.DataAnnotation which is open source and available here : <https://github.com/dotnet/corefx/tree/master/src/System.ComponentModel.Annotations/src>.

I could use an other validation API likeFLuent Validation, but this one is simple, well integrated into the Microsoft toolbelt (EFCore, ASPNET Core MVC) and in my opinion is enough.

The following pieces are already available :
- Attribute for specifying constraints (you can also create your own attribute)
- Utility class for validating constraints against an instance
- Api for writing error message

But for the front-end part we stil need to workon some stuff :
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

- The validation were already here because I already do server side validation

Then I create a service that will call the validation and because we are in a GUI app (where object are long lived), I am using C# event for propagating validation errors. I made this choice for making the developer experience easier : he won't have to manage the validator creation, just add the label on the right place.

```cs
 /// <summary>
/// Validate a given instance
/// </summary>
 public class ModelValidator : IModelValidator
    {
        /// <summary>
        /// Raised is a validation error occurs
        /// </summary>
        public event EventHandler<ValidationErrorEventArgs> OnValidationError;

        /// <summary>
        /// Validate the instance, if an error occurs returns false and raise the event
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <returns></returns>
        public bool Validate<T>(T instance)
        {
            List<ValidationResult> res = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(instance, new ValidationContext(instance, null, null), res, true);
            if (!isValid)
                OnValidationError?.Invoke(this, new ValidationErrorEventArgs() { Errors = res, Instance = instance });
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
The first step is to prevent a form submission when the model it tries to submit is not valid. For this I'll create a custom component with child content
