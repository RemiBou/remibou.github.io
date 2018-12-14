---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor,Azure DevOps, Nuget]
---
# How to create a Nuget package for Blazor assembly with Azure DevOps

A few weeks ago I found a way to validate my form on Blazor using DataAnnotations ([here](https://remibou.github.io/Client-side-validation-with-Blazor-and-Data-Annotations/)). In this blog post I'll explain how I created a package from it and shipped it as a nuget package.

## Solution

There is already a template for creating a Blazor librairy, so just create a project and type

```cmd
dotnet new blazorlib
```

This will create the project csproj and first files in the current directory. For my project I created the following solution structure :

- The librairy itself
- A sample
- A test project (empty for now)

This blazor lib is very interesting regarding the csproj produced. We can find the following lines :

```xml
<!-- .js/.css files will be referenced via <script>/<link> tags; other content files will just be included in the app's 'dist' directory without any tags referencing them -->
    <EmbeddedResource Include="content\**\*.js" LogicalName="blazor:js:%(RecursiveDir)%(Filename)%(Extension)" />
    <EmbeddedResource Include="content\**\*.css" LogicalName="blazor:css:%(RecursiveDir)%(Filename)%(Extension)" />
    <EmbeddedResource Include="content\**" Exclude="**\*.js;**\*.css" LogicalName="blazor:file:%(RecursiveDir)%(Filename)%(Extension)" />
```

This means that all the js and css included in the content folder will be automaticly loaded with your component in the client application, making it easy to reuse piece of js and css.

## Package definition

For defining your package you have to add some informations to your csproj file here is what I added in the first PropertyGroup tag :

```xml
<Authors>Rémi BOURGAREL</Authors>
<PackageTags>blazor validation dataannotation</PackageTags>
<RepositoryUrl>https://github.com/RemiBou/RemiBou.Blazor.DataAnnotation</RepositoryUrl>
<PackageProjectUrl>https://github.com/RemiBou/RemiBou.Blazor.DataAnnotation</PackageProjectUrl>
<Description>Blazor component for validating your form with DataAnnotations attributes. Made from this blog post https://remibou.github.io/Client-side-validation-with-Blazor-and-Data-Annotations/</Description>
```

- These are from here <https://docs.microsoft.com/en-us/dotnet/core/tools/csproj#nuget-metadata-properties>

## Azure DevOps and upload to nuget

For this step you need to have an account on [nuget.org](https://www.nuget.org/) and [Azure DevOps](https://dev.azure.com/). Then you register your nuget connection on your Service COnnection for your Azure DevOps account (see [here](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/service-endpoints?view=vsts)).

Here is my azure pipeline definition

```yml
trigger:
- master

pool:
  vmImage: 'vs2017-win2016'

variables:
  buildConfiguration: 'Release'

steps:
- task: DotNetCoreInstaller@0
  displayName: 'Install SDK'
  inputs:
    version: 2.2.100	
- task: NuGetToolInstaller@0
  inputs:
    versionSpec: '4.9.2' 
- task: DotNetCoreCLI@2
  displayName: 'dotnet pack'
  inputs:
    command: pack
    packagesToPack: RemiBou.Blazor.DataAnnotation/RemiBou.Blazor.DataAnnotation.csproj
    versioningScheme: byPrereleaseNumber
    configuration: $(buildConfiguration)
    majorVersion: '0' 
    minorVersion: '0' 
    patchVersion: '0'
    verbosityPack: Detailed
- task: NuGetCommand@2
  inputs:
    command: push
    nuGetFeedType: external
    publishFeedCredentials: 'Nuget'
```

- I had to rename my project from Blazor.RemiBou.DataAnnotation to RemiBou.Blazor.DataAnnotation because Microsoft reserved the prefix "Blazor." (error "409 This package ID has been reserved...").
- I have to update nuget because the runner uses the 4.1 and we need 4.8 for sending a net standard package (error " Unable to cast object of type 'System.String' to type 'NuGet.Frameworks.NuGetFramework")
- I use "byPrereleaseNumber" so every time I push a commit to my master branch a new version number is created as pre-release. I'll see later if this package needs release.
- I use dotnet pack because nuget pack wouldn't read well the csproj and ignore the dependencies.
- I use nuget push instead of dotnet push because dotnet push doesn't work with Service connections in Azure DevOps (error "DotNetCore currently does not support using an encrypted Api Key.")

After pushing on my branch and waiting a few minutes, my new package version appears here <https://www.nuget.org/packages/RemiBou.Blazor.DataAnnotation>.

## Using the package

On the project folder you type the command given by nuget.org

```cmd
dotnet add package RemiBou.Blazor.DataAnnotation --version 0.0.0-CI-20181214-215602
```

And on your project's _ViewImport.cshtml you add

```razor
@addTagHelper *, RemiBou.Blazor.DataAnnotation
```

So now you can use thepackage content like this

```razor
    <ValidatedForm OnSubmit="SubmitForm" Model="validatedInstance">
        <input type="text" bind="@validatedInstance.ValidatedField" /><br />
        <ValidationErrorLabel Model="validatedInstance" FieldName="ValidatedField" /><br />
        <button type="submit">Validate</button>
    </ValidatedForm>
```
## Conclusion

I had to do a lot of tries for making this works, the problems had more to do with Azure DevOps than Blazor, but still. Here again we can see again that Blazor for the client side is a nearly production ready technologies as it's compatible with already existing tools and workflows.

## Reference
- <https://docs.microsoft.com/en-us/dotnet/core/tools/csproj#nuget-metadata-properties>
- <https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package>
- <https://github.com/aspnet/Blazor.Docs/blob/master/docs/javascript-interop.md>