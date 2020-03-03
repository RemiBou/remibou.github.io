---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [ASPNET Core, Blazor, JSInterop]
---

# How to send callback method to JSInterop in Blazor ?

Blazor client-side or server side can handle only CPU bound calculation. For every interaction with the browser you need to use JSInterop. Even on Blazor itself the team uses JSInterop for using browser API like XHR request or DOM update.

Currently in the JS Interop API, you can do only one thing : call a given method connected to the "window" object with 3 kind of parameter :
- C# object serialized via System.Text.Json and deserialized via JSON.parse
- Html element (ElementReference in C# side)
- Reference to C# object on which you can then call any method annotated with "JSInvokable"

But what about passing a callback method, this would be useful if you want to register to an event like window.navigator.connection.onchange. With Blazor as-is, you can do it but you would have to do some plumbing and you would have to do it for every different callback you want to use. In this article I will show you how to do it in a more reusable way.

## Json Reviver

As I said previously Blazor JSInterop uses JSON.parse for creating an object from a JSON string send by the .net runtime. Even ElementReference or DotNetObjectReference are serialized in JSON and sended to this method, [here is the code that does it around line 217](https://github.com/dotnet/aspnetcore/blob/master/src/JSInterop/Microsoft.JSInterop.JS/src/src/Microsoft.JSInterop.ts) :

```ts
 function parseJsonWithRevivers(json: string): any {
    return json ? JSON.parse(json, (key, initialValue) => {
      // Invoke each reviver in order, passing the output from the previous reviver,
      // so that each one gets a chance to transform the value
      return jsonRevivers.reduce(
        (latestValue, reviver) => reviver(key, latestValue),
        initialValue
      );
    }) : null;
  }
```

[JSON.parse](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/JSON/parse) accepts a reviver parameter wich will be called for every field and every object of the json string. For instance for this js code

```js
const json = '{"result":true, "inner": {"count":42}}';
const obj = JSON.parse(json,(k,v) => {console.log(k,v); return v;} );
```

The console will output this

> "result" true
> "count" 42
> "inner" Object { count: 42 }
> "" Object { result: true, inner: Object { count: 42 } }

The reviver is called from the most nested property up to the root of the object. With this, the ASPNET Core team produced this reviver for changing a serialized ElementReference to the actual DOM element  ([file](https://github.com/dotnet/aspnetcore/blob/master/src/Components/Web.JS/src/Rendering/ElementReferenceCapture.ts)) :

```ts
export function applyCaptureIdToElement(element: Element, referenceCaptureId: string) {
  element.setAttribute(getCaptureIdAttributeName(referenceCaptureId), '');
}

function getElementByCaptureId(referenceCaptureId: string) {
  const selector = `[${getCaptureIdAttributeName(referenceCaptureId)}]`;
  return document.querySelector(selector);
}

function getCaptureIdAttributeName(referenceCaptureId: string) {
  return `_bl_${referenceCaptureId}`;
}

// Support receiving ElementRef instances as args in interop calls
const elementRefKey = '__internalId'; // Keep in sync with ElementRef.cs
DotNet.attachReviver((key, value) => {
  if (value && typeof value === 'object' && value.hasOwnProperty(elementRefKey) && typeof value[elementRefKey] === 'string') {
    return getElementByCaptureId(value[elementRefKey]);
  } else {
    return value;
  }
});
```

When you add @ref to an element in Blazor, it adds an id "_bl_NUMBER" to the HTML element. The number is stored inside the ElementReference struct, initialized from a static int incremented for each ref or GUID. This work is done in [this file](https://github.com/dotnet/aspnetcore/blob/master/src/Components/Components/src/ElementReference.cs) :

```cs
    public readonly struct ElementReference
    {
        private static long _nextIdForWebAssemblyOnly = 1;

        public string Id { get; }

        public ElementReference(string id)
        {
            Id = id;
        }

        internal static ElementReference CreateWithUniqueId()
            => new ElementReference(CreateUniqueId());

        private static string CreateUniqueId()
        {
            if (PlatformInfo.IsWebAssembly)
            {
                var id = Interlocked.Increment(ref _nextIdForWebAssemblyOnly);
                return id.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                return Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
            }
        }
    }
```

They also created a JsonConverter for this type for keeping Id with no setter.

## Custom Reviver and Func wrapper

Now we understand how they did it for ElementReference we can try to do it for Func. We need 2 thing :
- A C# wrapper that would keep a reference to my Func and a JSInvokable method that would call it
- A reviver that would detect that the object sended is a Func wrapper and call the C# method.

Here is my C# wrapper :

```cs
    public class CallBackInteropWrapper
    {
        [JsonPropertyName("__isCallBackWrapper")]
        public string IsCallBackWrapper { get; set; } = "";

        private CallBackInteropWrapper()
        {

        }
        public static CallBackInteropWrapper Create<T>(Func<T, Task> callback)
        {
            var res = new CallBackInteropWrapper
            {
                CallbackRef = DotNetObjectReference.Create(new JSInteropActionWrapper<T>(callback))
            };
            return res;
        }

        public static CallBackInteropWrapper Create(Func<Task> callback)
        {
            var res = new CallBackInteropWrapper
            {
                CallbackRef = DotNetObjectReference.Create(new JSInteropActionWrapper(callback))
            };
            return res;
        }

        public object CallbackRef { get; set; }


        private class JSInteropActionWrapper
        {
            private readonly Func<Task> toDo;

            internal JSInteropActionWrapper(Func<Task> toDo)
            {
                this.toDo = toDo;
            }


            [JSInvokable]
            public async Task Invoke()
            {
                await toDo.Invoke();
            }
        }

        private class JSInteropActionWrapper<T>
        {
            private readonly Func<T, Task> toDo;

            internal JSInteropActionWrapper(Func<T, Task> toDo)
            {
                this.toDo = toDo;
            }


            [JSInvokable]
            public async Task Invoke(T arg1)
            {
                await toDo.Invoke(arg1);
            }
        }
    }
```

- I need to do it in 2 wrapper class : one for holding the informaiton "this is a func wrapper" and one for holding the JSInvokable method.
- I created 2 variant : one where the Func accept an argument and the other where is doesn't
- I use Func&lt;Task&gt; so the callback can be asynchronous. I could also create overload for non Async callback but it would be too much noise.
- My fields have getter and setter which is bad, but System.Text.Json doesn't provide an easy way to make those private unless you create your own JsonConverter. The best way to fix this in an assembly would be to mark the type as internal and expose it as an interface.

Here is the reviver in js

```js
DotNet.attachReviver(function (key, value) {
    if (value &&
        typeof value === 'object' &&
        value.hasOwnProperty("__isCallBackWrapper")) {

        var netObjectRef = value.callbackRef;

        return function () {            
            netObjectRef.invokeMethodAsync('Invoke', ...arguments);
        };
    } else {
        return value;
    }
});
```

- DotNet.attachReviver is a method of the JSInterop js library
- "...arguments" means that I will send all the callback parameters to the "Invoke" method call as consecutive argument instead of an array of parameter.

SO for using this I declare this js function

```js
function testCallback(callback){
    if(confirm('are you sure ?')){
        callback("test");
    }
}
```

And call it like that in the .net side

```cs
await jsRuntime.InvokeVoidAsync("testCallback", CallBackInteropWrapper.Create<string>(s => Console.WriteLine(s)));
```

## BrowserInterop

Blazor without JSInterop is a bit hard because you often need to use some specific browser API : open a new window, get the geolocalization, get battery level etc ... so I though about creating a library called "[BrowserInterop](https://www.nuget.org/packages/BrowserInterop)" that would wrap all the JS Interop call regarding the browser API and I would avoid implementing all the API that could be implemented via Blazor (like onclick event or DOM API). You can have a look at the [GitHub repository](https://github.com/RemiBou/BrowserInterop) for getting an idea about what I mean.

During the development of this library I needed to implement window event handling (like "onclose" or "connection.onchange"), so I developped the technique described earlier and a a few more tools for avoiding .net developer to avoid writing js code as much as possible (I take a bullet for everyone if you prefer).

BrowserInterop provides the Wrapper I've explained beofre, you can use it like this :

```cs
var window = await jsRuntime.Window();
var eventListening = await window.OnMessage<string>(async (payload) => {
            onMessageEventPayload = payload;
            StateHasChanged();
            await Task.CompletedTask;
        });
```
- Window() is a BrowserInterop method which is the entrypoint to all the other interop methods, it also gives information about the window object
- OnMessage is an event handler for the "message" event on the window object. It's useful for cross window communication (a blog post will come about it)
- The event listening returns an IAsyncDisposible that once disposed will stop listening, so you can stop listening to event when your component is disposed just like with C# event. 
- Thanks to BrowserInterop, I can read the message. In "vanilla" JSInterop you would have an empty object because informations in the "message" event payload are not serialized when sent to JSON.stringify, more about this on an other blog post.


## Conclusion

Even though C# developer dream (never touch JS againà) is becoming true with Blazor, you still need to do someplumbing for talking with the browser. Let's hope that some more library will remove this need in the future and maybe one day we'll be able to use Browser API directly with WebAssembly.
