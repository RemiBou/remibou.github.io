# How I migrated protogen to Blazor

Protogen (https://protogen.marcgravell.com/) is a web app for working with the serialization format protobuf. It provides two features :
  - Generating code from .proto file (which describes data structure and API)
  - Reading serialized data (binary) and displaying it in a human readable form

The current website works well, but a few weeks ago Marc Gravell, creator of [protobufnet](https://github.com/protobuf-net/protobuf-net), asked on Twitter if someone could migrate it to Blazor. I opened a [issue](https://github.com/protobuf-net/protobuf-net/issues/546) in GitHub, asked a few questions and started having fun.

The main interest in migrating it to Blazor is to try to execute most of the logic on the client side and go to server side only when needed :
- The code generation in C# and VB.net can be done client side because protobufnet targets netstandard. But the code generation for other languages (php, python ...) uses an executable called "protoc" from Google written in C++, so this part needed to be kept on the server side. I could've tried to execute it on the client with some C++ to wasm compiler but it was a bit too much for now.
- The binary reading can be done client side as the library  targets netstandard.

## The POC

Even if protobufnet targets netstandard, I was still not sure I could execute the code generation on the client-side. I created this Proof Of Concept (in this [commit](https://github.com/RemiBou/protobuf-net/commit/1f6ce4fe6cad89fcebbc295f4bac683d62d021a4)) for validating the first idea

```cs
@using System.Net.Http
@using  Google.Protobuf.Reflection
@using System.IO
@using  ProtoBuf.Reflection
@inject HttpClient Http

@page "/"

<h1>Hello, world!</h1>

Welcome to your new app.
@foreach(var file in  codeFiles){
    <b>FILE = @file.Name</b><br/>
    <b>CONTENT =</b> 
    <pre>
        @file.Text
    </pre>
}
@if(codeFiles.Count == 0){
    <b>NO CODE FILE</b>
}

@code{

    private List<CodeFile> codeFiles = new List<CodeFile>();
    protected override async Task OnInitializedAsync()
    {
        var schema = await Http.GetStringAsync("https://raw.githubusercontent.com/protobuf-net/protobuf-net/4b239629f5f9dbe4770a497f2c81465ab0669504/src/protobuf-net.Test/Schemas/descriptor.proto");
        using (var reader = new StringReader(schema))
        {
            var set = new FileDescriptorSet
            {
                ImportValidator = path => true,
            };
            set.Add("my.proto", true, reader);

            set.Process();
            CodeGenerator codegen = CSharpCodeGenerator.Default;
            codeFiles = codegen.Generate(set, NameNormalizer.Default).ToList();
        }
    }
}
```

- This was created in a brand new blazor app with a reference to protobufnet
- It loads a sample .proto file from github

And ... it did not build because of this error

```
Fatal error in IL Linker

  Unhandled Exception: Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly: 'System.Private.ServiceModel, Version=4.5.0.3, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
```

This error is caused by the ILLinker. The linker browse your whole code and tries to find useless part in assemblies and remove it so the application binary size get smaller. The consequences of such browsing is that if illinker cannot find one of your transitive dependency (dependency of a dependency), it'll throw an error. Here the protobufnet dependencies requires System.Private.ServiceModel which the linker cannot found (it might be a bug that'll get resolved once Blazor package embed the latest illinker, [source](https://github.com/mono/linker/issues/604)). 

As a first step I disabled the linker with the following tag in my csproj and it worked.

```xml
    <BlazorLinkOnBuild>false</BlazorLinkOnBuild>
```

Then Marc told me he split protobufnet in two part protobuf-net (all thepart with dependencies to System.ServiceModel.Primitives) and protobuf-net.Core. I don't think he did it for me but it helped greatly as I could change my dependency to .Core and enable the linker, which reduced the app size from 7.3MB to 2.7MB :)

Once this was done, the POC was working nicely, so I decided to move on and finish the job.

## Architecture

I decided to go for a classic ASPNET Core API that would host my Blazor CLient side app because I still needed a little API, and that would keep the deployment simple as it is now (I guess Marc does a right-click publish from his Visual Studio as there is no pipeline defined on the repo).

I kept the existing protogen.site project and created a protogen.site.blazor.client besides it. I had to add the reference to the blazor project from the server project :

```xml
    <ProjectReference Include="..\protogen.site.blazor.client\protogen.site.blazor.client.csproj" />
```

And add this lines to the Startup.cs of the api project

```cs
public void ConfigureServices (IServiceCollection services) {
    // ...

    services.AddResponseCompression (opts => {
        opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat (
            new [] { "application/octet-stream" });
    });
}
```

This enables compression for the dll files, which is not enabled by default. Most of the time compression on binary is not efficient but for .net binary it is very efficient (an average of -60% in size).

```cs
public void Configure (IApplicationBuilder app) {
    app.UseResponseCompression ();
    app.UseStaticFiles ();
    app.UseClientSideBlazorFiles<ProtoBuf.Startup> ();
    app.UseRouting ();
    app.UseEndpoints (endpoints => {
        endpoints.MapDefaultControllerRoute ();
        endpoints.MapFallbackToClientSideBlazor<ProtoBuf.Program> ("index.html");
    });
}
```
- UseClientSideBlazorFiles will be used for returning css and js files from my Blazor project embedded ressources
- MapFallbackToClientSideBlazor will return my Blazor project index.html file if ASPNET Core is not able to map the request to an existing API. This is classic behavior for SPA project.

I also had to remove all the static files (js/css) from the API project because if their url matched the files in the Blazor project then they would be returned to the browser. I also could've remove the UseStaticFiles().

Now that my two projects are working correctly together I need to create the first part of the app : the code generation.

## Code generation

The code generation is pretty simple : you have a .proto file as input, you choose the language and it creates the code files you need, here is a screenshot of what it looks like at the end :

![Protogen generator](/assets/img/protogen_generator.PNG "Protogen generator")

For the layout I tried to do like other online code generator : split screen with syntax highlight (more on this later).

I really like the MVVM pattern as it makes it really easy to have a bit of complexity for managing your UI whil still having two way binding between your model and your view. Here is the GeneratorViewModel

```cs
namespace ProtoBuf.Models {
    public class GeneratorViewModel {
        private GeneratorLanguageEnum language;

        public enum GeneratorLanguageEnum {
            CSharp,
            CSharpProtoc,
            VBNet,
            CPlusPlus,
            Java,
            JavaNano,
            JS,
            Objc,
            PHP,
            Python,
            Ruby
        }
        public enum NamingConventionEnum {
            Auto,
            Original
        }
        private static Dictionary<GeneratorLanguageEnum, IEnumerable<string>> LanguageVersions { get; set; } = new Dictionary<GeneratorLanguageEnum, IEnumerable<string>> { { GeneratorLanguageEnum.CSharp, new [] { "7.1", "6", "3", "2" } },
            { GeneratorLanguageEnum.VBNet, new [] { "vb14", "vb11", "vb9" } }
        };

        [Required]
        public GeneratorLanguageEnum Language {
            get => language;
            set {
                language = value;
                LanguageVersion = null;
            }
        }
        public bool? OneOfEnum { get; set; } = false;
        public bool? RepeatedEmitSetAccessors { get; set; } = false;

        public string LanguageVersion { get; set; }
        public NamingConventionEnum NamingConvention { get; set; } = NamingConventionEnum.Auto;

        public NameNormalizer GetNameNormalizerForConvention () {
            switch (NamingConvention) {
                case NamingConventionEnum.Auto:
                    return NameNormalizer.Default;
                case NamingConventionEnum.Original:
                    return NameNormalizer.Null;
                default:
                    throw new ArgumentOutOfRangeException (nameof (NamingConvention));
            }
        }

        public CodeGenerator GetCodeGenerator () {
            if (!IsProtobugGen ()) {
                throw new InvalidOperationException ("CodeGenerator are available only for language compatible with protobuf-net");
            }
            switch (Language) {
                case GeneratorLanguageEnum.CSharp:
                    return CSharpCodeGenerator.Default;
                case GeneratorLanguageEnum.VBNet:
                    return VBCodeGenerator.Default;
                default:
                    throw new ArgumentOutOfRangeException ($"{Language} is not supported");
            }
        }

        public Dictionary<string, string> GetOptions () {
            var res = new Dictionary<string, string> ();
            if (LanguageVersion != null) {
                res.Add ("langver", LanguageVersion);
            }
            if (OneOfEnum.GetValueOrDefault (false)) {
                res.Add ("oneof", "enum");
            }
            if (RepeatedEmitSetAccessors.GetValueOrDefault (false)) {
                res.Add ("listset", "yes");
            }
            return res;
        }

        public bool IsProtobugGen () {
            return Language == GeneratorLanguageEnum.CSharp ||
                Language == GeneratorLanguageEnum.VBNet;
        }
        public string GetMonacoLanguage () {
            //taken from here https://github.com/microsoft/monaco-languages
            switch (Language) {
                case GeneratorLanguageEnum.VBNet:
                    return "vb";
                case GeneratorLanguageEnum.CSharp:
                case GeneratorLanguageEnum.CSharpProtoc:
                    return "csharp";
                case GeneratorLanguageEnum.CPlusPlus:
                    return "cpp";
                case GeneratorLanguageEnum.JavaNano:
                case GeneratorLanguageEnum.Java:
                    return "java";
                case GeneratorLanguageEnum.JS:
                    return "js";
                case GeneratorLanguageEnum.Objc:
                    return "objective-c";
                case GeneratorLanguageEnum.PHP:
                    return "php";
                case GeneratorLanguageEnum.Python:
                    return "python";
                case GeneratorLanguageEnum.Ruby:
                    return "ruby";
                default:
                    throw new ArgumentOutOfRangeException ($"{Language} is not supported by protoc");
            }
        }
        public string GetProtocTooling () {

            switch (Language) {
                case GeneratorLanguageEnum.CSharpProtoc:
                    return "csharp";
                case GeneratorLanguageEnum.CPlusPlus:
                    return "cpp";
                case GeneratorLanguageEnum.Java:
                    return "java";
                case GeneratorLanguageEnum.JavaNano:
                    return "javanano";
                case GeneratorLanguageEnum.JS:
                    return "js";
                case GeneratorLanguageEnum.Objc:
                    return "objc";
                case GeneratorLanguageEnum.PHP:
                    return "php";
                case GeneratorLanguageEnum.Python:
                    return "python";
                case GeneratorLanguageEnum.Ruby:
                    return "ruby";
                default:
                    throw new ArgumentOutOfRangeException ($"{Language} is not supported by protoc");
            }
        }

        [Required]
        public string ProtoContent { get; set; }

        public bool HasLanguageVersion () {
            return LanguageVersions.ContainsKey (Language);
        }
        public IEnumerable<string> GetLanguageVersions () {
            return LanguageVersions[Language];
        }
    }
}
```

I tried to implement most of the logic here for keeping the razor part of my view clean of any decision.

The rest of the generation razor file can be found [here](https://github.com/RemiBou/protobuf-net/blob/protobufgen-blazor/src/protogen.site.blazor.client/Pages/Index.razor).

I implemented on this page the decision wether I call the server or not : I call the server for every non .net language but also for .net languages when there is an include in the .proto file because protogen has some usual include available on server side and, because there is no abstraction over the file system access, I couldn't run it on client side where there is no file system :(.

The nice thing with Blazor is that I can send my view model instance to the server (serialized as Json) and call the same method on the client and the server, which reduce code duplication / translation (when you implement the same thing in js and in cs).

## Monaco integration

In the previous version of protogen there was a code editor called "monaco" that was integrated. Monaco is the code editor part of visual studio and can be used standalone on a web page. Integrating monaco was not easy because I needed to learn how to enable it and then how to enable the two way binding between my view model string property and monaco.

When doing this kind of project I prefer to use CDN resources for js library. Mostly because it removes the pain of dealing with npm or other mess like bower or webpack and because someone must be paying for this bandwidth and I prefer when it's someone else. Here is the code for initializing monaco from a cdn

```html
    <script src="https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.18.0/min/vs/loader.js"></script>
```
```js
require.config({ paths: { 'vs': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.18.0/min/vs' } });
window.MonacoEnvironment = {
    getWorkerUrl: function (workerId, label) {
        return `data:text/javascript;charset=utf-8,${encodeURIComponent(`
                                            self.MonacoEnvironment = {
                                              baseUrl: 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.18.0/min/'
                                            };
                                            importScripts('https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.18.0/min/vs/base/worker/workerMain.js');`
        )}`;
    }
};
```
This was taken mostly from the monaco repository.

Now I need to enable monaco on a given block with JSInterop

MonacoEditor.cshtml : 
```cs
@inject IJSRuntime JsRuntime;
<div class="h-100" style="border: 1px solid grey" @ref="editor"></div>
@code {
    private bool _initDone = false;

    ElementReference editor;

    [Parameter]
    public string Language { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; } = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JsRuntime.InvokeVoidAsync("initMonaco",
                editor,
                DotNetObjectReference.Create(this),
                Language,
                ReadOnly);
            _initDone = true;
        }
    }
}

```
- "editor" will hold a reference to the dom element of the div when I call JSInterop.
- I have to call the initialization of monaco in the lifecycle method OnAfterRenderAsync because in OnInitialized, editor will not exists in the DOM and the js call will fail
- I am not sure why I need to handle _initDone
- I send a reference to "this" in the init so I will be able to listen for change in monaco and then call back the Blazor component for refreshing the field

The js part

```js
window.initMonaco = function (block, component, language, readonly) {
    if (!block) {
        return;
    }

    block.monacoEditorModel = monaco.editor.createModel("", language);
    block.monacoEditorModel.onDidChangeContent(function (e) {
        component.invokeMethodAsync('OnEditorValueChanged', block.monacoEditorModel.getValue());
    });
    block.monacoEditor = monaco.editor.create(block, {
        language: language,
        minimap: {
            enabled: false
        },
        readOnly: readonly,
        automaticLayout: true,
        scrollBeyondLastLine: false,
        model: block.monacoEditorModel
    });

};
```

- I create a monaco editor model for future usage
- We can see in the call to onDidChangeContent the call to the .net method for refreshing the value

Now here is the rest of the monaco editor for handling two way binding

```cs
private string _content;
    private bool _initDone = false;

    ElementReference editor;

    [Parameter]
    public string Language { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; } = false;

    [Parameter]
    public string Content
    {
        get
        {
            return _content;
        }
        set
        {
            if(_content == value)
            {
                //this break the SO exception : set content will trigger editor changed 
                //value that'll trigger the binded value change that'll call this method ...
                //this is not supposed to happen in chain binding, but it does because the jsinterop involved here 
                return;
            }
            _content = value;
            SetContent(value);
        }
    }
    [Parameter]
    public EventCallback<string> ContentChanged { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      // ...
            if (!string.IsNullOrEmpty(Content))
            {
                SetContent(Content);
            }
    }

    public void SetContent(string content)
    {
        if (_initDone) // the component is not initialized, we ignore the setvalue as it'll be done in OnAfterRenderAsync
            ((IJSInProcessRuntime)JsRuntime).InvokeVoid("setMonaco", editor, content);
    }
    [JSInvokableAttribute("OnEditorValueChanged")]
    public async Task OnEditorValueChanged(string content)
    {
        this._content = content;
        await ContentChanged.InvokeAsync(content);
    }

    public async Task<string> GetContent()
    {
        return await JsRuntime.InvokeAsync<string>("getMonaco", editor);
    }
```

- If you want to handle two way binding with a custom component, you must add a Parameter of type EventCallBack<XX> name "MyFieldChanged" where XX is the bounded field typeand MyField is the name of the parameter and then call the method "InvokeASYNC3 on this instance when the value changes
- I had to handle AN infinite loop because calling the callback would execute a set on my property which would then call the set of "Content" which would then change the monaco editor value which would then trigger onDidChangeContent ....

This code was long to create because I never done it, and the debug experience is not optimal when dealing with jsinterop. A lot of console.log and Console.WriteLine were done :p

One last thing I needed to do with monaco was to handle syntax error in the .proto file. Monaco provides an API for this. I had to create the js wrapper method and the JS Interop call :

```js
 window.addMonacoError = function (block, lineNumber, lineEnd, columnNumber, columnEnd, message, isError) {
    var existingErrors = [];
    if (block.monacoErrors) {
        existingErrors = block.monacoErrors;
    }
    existingErrors.push({
        startLineNumber: lineNumber,
        startColumn: columnNumber,
        endLineNumber: lineEnd,
        endColumn: columnEnd,
        message: message,
        severity: isError ? monaco.MarkerSeverity.Error : monaco.MarkerSeverity.Warning
    });
    monaco.editor.setModelMarkers(block.monacoEditorModel, "owner", existingErrors);
    block.monacoErrors = existingErrors;
};
window.cleanMonacoError = function (block) {
    block.monacoErrors = [];
    monaco.editor.setModelMarkers(block.monacoEditorModel, "owner", []);
};
```

Underlining a part of the code is called "model markers" in monaco, most of the work here was to find the right API in monaco doc and repository.

And the interop code

```cs
public async Task AddError(bool isError, int lineNumber, int lineEnd, int columnNumber, int columnEnd, string text)
{
    await JsRuntime.InvokeAsync<string>("addMonacoError", editor, lineNumber, lineEnd, columnNumber, columnEnd, text, isError);

}

public async Task ClearErrors()
{
    await JsRuntime.InvokeVoidAsync("cleanMonacoError", editor);

}
```

For displaying the syntax error I have to validate the .proto file with API from protobufnet, then call the AddError on the MonacoEditor instance

```cs
<MonacoEditor @ref="protoEditor" Language="protobuf" @bind-Content="model.ProtoContent"></MonacoEditor>

@code{
    MonacoEditor protoEditor;

    protected async Task Generate()
    {
        try
        {
        //...
            await protoEditor.ClearErrors();
            StateHasChanged();
            using (var reader = new StringReader(model.ProtoContent))
            {
                var set = new FileDescriptorSet
                {
                    ImportValidator = path => true
                };
                set.Add("my.proto", true, reader);

                set.Process();
                errors = set.GetErrors();
                if (errors.Any())
                {
                    foreach (var error in errors)
                    {
                        await protoEditor.AddError(error.IsError, error.LineNumber, error.LineNumber, error.ColumnNumber, error.ColumnNumber + error.LineContents.Length, error.Message);
                    }
                    return;
                }
                //generate the code
            }
        }
    }
}
```

Most of this code was already implemented in js in the current version of protogen, I translated it to protobuf.

This monaco component is really easy to integrate so I reused it for displaying code generation result

```cs
@if (codeFiles != null)
{
    <ul class="nav nav-tabs mb-2">
        @foreach (var file in codeFiles)
        {
            <li class="nav-item">
                <a class="nav-link  @(currentCodeFile == file ? "active":"")" href="#" @onclick="() => currentCodeFile = file">  @file.Name</a>
            </li>
        }
    </ul>
    <div class="form-group flex-grow-1  d-flex flex-column">
        @if (currentCodeFile != null)
        {

            <MonacoEditor Language="@model.GetMonacoLanguage()" Content="@currentCodeFile.Text" ReadOnly="true">
            </MonacoEditor>
        }
    </div>
}
```

The two way binding is useless here as it's readonly but display the code with syntax highligh gives a way better user experience.

## Binary parser
The second part of protogen is the binary parser, here is what it looks like now

![Protogen Parser](/assets/img/Protogen_parser.PNG "Protogen Parser")

There is not much to say about the parser but :
- At first I used my code from a previous blog post for file reading, but it was slow. So I decided to use the [InputFile from Steve Sanderson](https://blog.stevensanderson.com/2019/09/13/blazor-inputfile/) (one of the person at the origin of Blazor) which is waaaaay better.
- It's really easy to migrate a server side razor (cshtml) to a blazor component : you just have to remove the helpers but the syntax is the same. I started without understanding anything about the parser, I copy/paste the content of the cshtml, replace Partial with reference to my component, removed the helper and voila, it was working like a charm.

There is still an error during the parsing of some binary but I am not sure it has anything to do with my work.

## Conclusion

This was a nice project, I learned a lot about Blazor and protobufnet. I hope this tool will make it easier to use protobufnet, I also hope it'll make Marc's and over protobuf user life easier (I think it's the only online code generator for protobuf) and maybe do some nice publicity for Blazor.

This work is not online. I hope it'll be one day if Marc agrees with my work (he is doing a massive refactoring right now, so the MR won't be submited right now).

The full code is available [here](https://github.com/RemiBou/protobuf-net/tree/protobufgen-blazor/src/protogen.site.blazor.client)


