# Validate your Blazor form using the EditForm

Blazor server-side will be released with ASPNET Core 3, with this release the ASPNET team worked on implementing form validation, so anyone could implement it's own validation logic and the framework would take carre of the rest : blocking form subit, adding / removing css class, displaying error message ... They also implemented the first validator for classes annotated with DataAnnotation attribute.

Most of this is implemented by this PR <https://github.com/aspnet/AspNetCore/pull/7614> which was merged 6 weeks ago. This code was made available on Blazor 0.9 one month ago.

I already wrote [my own form validation logic](https://remibou.github.io/Client-side-validation-with-Blazor-and-Data-Annotations/) but their solution is way better as it requires less plumbing : you add the model reference only once (at the form level) then all the child component will know about it via the EditContext.

In this blog post we'll explore their work, how to use it and how to customize it.

## Blazor form validation component

The form validation is implemented mostly on the namespace "Microsoft.AspNetCore.Components.Forms" the source code is located (here)[https://github.com/aspnet/AspNetCore/tree/master/src/Components/Components/src/Forms] (Components will be renamed back to Blazor before the 3.0 release).
The main class, I think, you should know about are :
- [Microsoft.AspNetCore.Components.Forms.EditContext](https://github.com/aspnet/AspNetCore/blob/master/src/Components/Components/src/Forms/EditContext.cs) : This class group the validation information (validator, message, fields) for one instance. This is the integration point of your custom validation, by subscribing to its event, you can execute your own validation logic and send your error to the GUI.
- [Microsoft.AspNetCore.Components.Forms.EditForm](https://github.com/aspnet/AspNetCore/blob/master/src/Components/Components/src/Forms/EditForm.cs) : This component is a html form tag that'll instantiate the EditContext for a given instance, it also provides event for submiting your form only when he validation succeeds.
- [Microsoft.AspNetCore.Components.Forms.ValidationMessageStore](https://github.com/aspnet/AspNetCore/blob/master/src/Components/Components/src/Forms/ValidationMessageStore.cs) : This class is used for adding error about a field to an EditContext. 

Here is how the validation is executed :
- The EditForm instantiate the EditContext with themodel instance you gave it.
- Services are created by you or some framework components and listen to the EditContext event, they have to create a ValidationMessageStore for pushing errors to the EditContext (it does provide a public API for this)
- When the form is submited, EditForm calls Validate on the EditContext
- EditContext triggers the event OnValidationRequested with itself as parameter
- Every Services who are listening to this instance event will do their validation work and push error to the message store.
- When you push an error to the message store, it creates a field reference on the EditContext, link itself to this field (internal class FieldState) and store the error message on a map.
- Once every listenner has done its job, the EditContext browse all the fields states, and check if there is any error. If there is one error then the submit callback is not called.

I don't know if I am clear enough here, I hope it'll be clearer by the end of this post.

## Validate your form

Here is a register form validated via the data annotation attributes

```cs
        <EditForm  OnValidSubmit="CreateAccount" Model="@registerCommand" ref="registerForm">
            <DataAnnotationsValidator />
            <div asp-validation-summary="All" class="text-danger"></div>
            <div class="form-group row mb-1">
                <label class="col-sm-3 col-form-label" for="NewEmail">Email</label>

                <div class="col-sm-9">
                    <InputText Class="form-control" bind-Value="@registerCommand.Email" />
                    <ValidationMessage For="@(() => registerCommand.Email)"/>
                </div>

            </div>
            <div class="form-group row mb-1">
                <label class="col-sm-3 col-form-label" for="NewName">Name</label>

                <div class="col-sm-9">
                    <InputText Class="form-control" bind-Value="@registerCommand.Name" />
                     <ValidationMessage For="@(() => registerCommand.Name)" />
                </div>
            </div>
            <div class="form-group row mb-1">
                <label class="col-sm-3 col-form-label" for="NewPassword">Password</label>

                <div class="col-sm-9">
                    <InputPassword Class="form-control" bind-Value="@registerCommand.Password" />
                     <ValidationMessage For="@(() => registerCommand.Password)" />
                </div>
            </div>
            <div class="form-group row mb-1">
                <label class="col-sm-3 col-form-label" for="NewConfirmPassword">Confirm</label>

                <div class="col-sm-9">
                    <InputPassword Class="form-control" bind-Value="@registerCommand.ConfirmPassword" />
                    <ValidationMessage For="@(() => registerCommand.ConfirmPassword)" />
                </div>
            </div>
            <div class="form-group text-center mb-0">
                <button type="submit" ref="createButton" id="BtnRegister" class="btn btn-primary">Register</button>
             
            </div>  

        </EditForm>
```

- I use EditorForm instead of plain hmtl form
- InputText is used for binding your input to the validation lgoic, it'll be executed when you edit the value. The invalid css class will be added if the field is invalid, valid will be added if it's not.
- ValidationMessage displays the error message for the given field in a div with the class validation-message. You also have ValidationSUmmary if you want to display all your message on the same place.
- I found a bug the way it handles the CompareAttribute, I will try to fix this and send a PR.
- InputPassword is my own, as the ASPNET Team decided to provide only a limited set of input attribute via the build-in components. It's not a big problem because creating this component is as simple as this :
  
```cs
@inherits InputBase<string>
<input bind="@CurrentValue" type="password" id="@Id" class="@CssClass" />

@functions{
        protected override bool TryParseValueFromString(string value, out string result, out string validationErrorMessage)
        {
            result = value;
            validationErrorMessage = null;
            return true;
        }
}
```

Maybe when something like Angular decorator will be available in Blazor it'll be simpler, but so far it's not a big deal.

I also added the folowing css for applying Bootstrap styling to the errors

```css
 .form-control.invalid{
    border-color:#dc3545;
}
.form-control.valid{
    border-color:#28a745;
}
.validation-message {
    width: 100%;
    margin-top: .25rem;
    font-size: 80%;
    color: #dc3545;
}
```

Now when submiting the form or changing an input value, the fields are red and the error messages are displayed like this

![Form validation]({{ site.url }}/assets/img/FormValidation.png)

## Display validation error from the server

Personally I don't like to handle validation about the global state (like uniqueness of an email) with validation attribute, I prefer to handle it explicitly on my command handler. So it can happen that my server returns validation error. For returning those error from the server I simply build a Dictionary&lt;string,List&lt;string&gt;&gt; where the key is the field name and the values are the error message from the server side and return it with a bad request (400) Http status. You can checkout my project Toss how I do it on the server side here : <https://github.com/RemiBou/Toss.Blazor>.

On the client side, I first have to plug my custom valdiator to the EditContext. This is my validator :

```cs
public class ServerSideValidator : ComponentBase
{
    private ValidationMessageStore _messageStore;

    [CascadingParameter] EditContext CurrentEditContext { get; set; }

    /// <inheritdoc />
    protected override void OnInit()
    {
        if (CurrentEditContext == null)
        {
            throw new InvalidOperationException($"{nameof(ServerSideValidator)} requires a cascading " +
                $"parameter of type {nameof(EditContext)}. For example, you can use {nameof(ServerSideValidator)} " +
                $"inside an {nameof(EditForm)}.");
        }

        _messageStore = new ValidationMessageStore(CurrentEditContext);
        CurrentEditContext.OnValidationRequested += (s, e) => _messageStore.Clear();
        CurrentEditContext.OnFieldChanged += (s, e) => _messageStore.Clear(e.FieldIdentifier);
    }

    public void DisplayErrors(Dictionary<string, List<string>> errors)
    {
        foreach (var err in errors)
        {
            _messageStore.AddRange(CurrentEditContext.Field(err.Key), err.Value);
        }        
        CurrentEditContext.NotifyValidationStateChanged();
    }
}
```

- It's a component as it'll have to be inserted on the componenthierarchy for getting the casscading EditContext from the form
- As said before I have to create a ValidationMessageStore for pushing errors to the context
- I clean the error when a field is edited so the user can retry an other value

For using this I have to add this component under my form like this

```cs
        <EditForm  OnValidSubmit="CreateAccount" Model="@registerCommand" ref="registerForm">
            <DataAnnotationsValidator />
            <ServerSideValidator ref="serverSideValidator"/>
            ....
        </EditForm>
        ...

    @functions{
        RegisterCommand registerCommand = new RegisterCommand();
        ServerSideValidator serverSideValidator;
        async Task CreateAccount(EditContext context)
        {
            await ClientFactory.Create("/api/account/register", createButton)
                .OnBadRequest<Dictionary<string, List<string>>>(errors => {

                    serverSideValidator.DisplayErrors(errors);
                })
                .OnOK(async () =>
                {
                    await JsInterop.Toastr("success", "Successfully registered, please confirm your account by clicking on the link in the email sent to " + registerCommand.Email);
                    registerCommand = new RegisterCommand();
                    StateHasChanged();
                })
                .Post(registerCommand);
        }
    }
```
  - I use the "ref" keyword for itneracting directly with the validator
  - The field name pushed by the server must match the field name of the command
  - With this if the user name or email are not unique, the message will be displayed beneath the good field.


## Conclusion

I don't know if this is the best way to do something like that but it works, and it's not very complicated. The approach taken by the ASPNET Team is quite good as you don't have to implement an adapter interface, you just have to listen to the event you want to use. 

The good thing here, just like with the first blog post, is that I don't have to implement twice some validaiton mechanism : I just add my Data Annotation attributes to my command/query class and they'll be validate don both the client and the server.

The next step might be to implement custom validator using services that would work on both side.

