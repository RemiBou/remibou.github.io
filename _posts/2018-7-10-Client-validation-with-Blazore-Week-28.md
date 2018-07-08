# Client validation with Blazor and Data Annotations
CLient side validation can be useful for 1 reason : performance. With client side validation you can reduce the number of server requests by making first check on the browser side : field is not empty, checkbox selected  ...

But IMHO it's not mandatory (server side is) and so shouldn't take long to setup/configure, you can create a project withoutany client-side validation and add some later as you determine which checks are the most important.

This type of validation was really well integrated in ASPNET Core : 
- you add DataAnotation attributes to your model/ view model class
- you use HtmlHelper for generating your html
- you reference jquery unobstruvie validation library
- you're good to go

This kind of developer experience would be great with Blazor, the only problem being that we don't have HtmlHelper. But DataAnnotation is compatible with .net standard 2.0 so if we look at the source code we might be able to do what the helpers are doing.

## Data Annotation
This part of the NETCORE frameworkis defined [here](https://github.com/dotnet/corefx/tree/master/src/System.ComponentModel.Annotations/src/System/ComponentModel/DataAnnotations). It helps developers defining rules for client and server side validation with attributes like this

```cs
public class MyModel{
  [Required]
  public string Test{get;set;}
}
```

This means that if an Mvc action receives an instance of MyModel as parameter without the Test property set, then the call "Model.IsValid" will return false and the Controller property "Model" will be filled with (localized) error details. And on the client side, it will prevent form submiting if there is a field for the property Test and no value are typed. 

There is a lot of built in attributes, and you can also define yours. Those attributes might also be used by other tools / frameworks such as Entity Framework Core, for instance the Required attribute will mean that this field is non nullable.

##  Html Helpers

The ASPNET Core Html Helpers are here for translating those attribute in html attribute that'll be read by jQuery Unobstrusive Validation and if possible in html5 attribute (eg :"required").

These Helpers are defined [here](https://github.com/aspnet/Mvc/tree/4f1f97b5d524b344c34a25a7031691626d50ec68/src/Microsoft.AspNetCore.Mvc.ViewFeatures). They [target netstandard 2.0](https://github.com/aspnet/Mvc/blob/4f1f97b5d524b344c34a25a7031691626d50ec68/src/Microsoft.AspNetCore.Mvc.ViewFeatures/Microsoft.AspNetCore.Mvc.ViewFeatures.csproj), so in theory we can use it in blazor.

## Finding the place where everything happens

Searching for a specific part of code in a large code base written by people you don't know can be challenging as everything is not documented and everyone/every team/organisation in the world makes architectural mistakes, but you have to find clues and follow them. Her eI chose to follow the static property "HtmlHelper.ValidationInputCssClassName" , the attribute construction must happen where it's used right ? You can checkout the project and browse it with Visual Studio or directly in Github, it's easier in VS most of the time. So here is my path

1. Clone the repo
2. [HtmlHelper.cs](https://github.com/aspnet/Mvc/blob/4f1f97b5d524b344c34a25a7031691626d50ec68/src/Microsoft.AspNetCore.Mvc.ViewFeatures/ViewFeatures/HtmlHelper.cs) then search ref to "ValidationInputCssClassName"
3. Found [DefaultHtmlGenerator](https://github.com/aspnet/Mvc/blob/4f1f97b5d524b344c34a25a7031691626d50ec68/src/Microsoft.AspNetCore.Mvc.ViewFeatures/ViewFeatures/DefaultHtmlGenerator.cs), the method AddValidationAttributes line 1384 got my attention but it's protected, so I can't use it, and maybe it's doing too muche for what I need s I got to dig deeper. It's calling a method "AddAndTrackValidationAttributes" on some validationAttributeProvider, I guess this adds entry to the attributes in TagHelper.
4. This method implementaiton I think is [this one](https://github.com/aspnet/Mvc/blob/4f1f97b5d524b344c34a25a7031691626d50ec68/src/Microsoft.AspNetCore.Mvc.ViewFeatures/ViewFeatures/DefaultValidationHtmlAttributeProvider.cs)



