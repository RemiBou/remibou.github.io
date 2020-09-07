---

layout: post

feature-img: "assets/img/pexels/circuit.jpeg"

tags: [ASPNET Core, Blazor, BrowserInterop]

---



# How to get the user GPS coordinates with Blazor using BrowserInterop?



I already said it, Blazor is [awesome](https://remibou.github.io/Should-I-peek-Blazor/). But it got downside. One of these downside comes from the fact that it relies on WebAssembly. WebAssembly doesn't provide browser API like the [Geolocation API](https://developer.mozilla.org/en-US/docs/Web/API/Geolocation_API). For getting a user GPS coordinates (with his/her approval of course), we need to use JSinterop so we can call the Geolocation API. Because integrating these methods can be tedious I created a package called [BrowserInterop](https://www.nuget.org/packages/BrowserInterop/) which implements JSInterop call for many browser API. You can have a look at the repo [WIKI](https://github.com/RemiBou/BrowserInterop/wiki/Browser-API-covered) to see the implemented methods. 



In this blog post, I will explain how to use this library for getting the user's geolocation and for knowing when the user moves.



## Setup



First, create a Blazor WASM project (this will also work in Blazor Server side). 

In your project folder type :



```bash

dotnet new blazorwasm

```



Then reference the BrowserInterop library :



```bash

dotnet add package BrowserInterop --version 1.1.1

```



Then on your index.html add a reference to the required js code (after the webassembly.js tag) :



```html

 <script src="_content/BrowserInterop/scripts.js"></script>

```



You can find all the blog post source code [here](https://github.com/RemiBou/remibou.github.io/tree/master/projects/RemiBou.BlogPost.Gps).



## Getting the current position



Now in one of your page, you need the current IJSRuntime :



```razor

@inject IJSRuntime jsRuntime

```



Add a reference to the BrowserInterop namespace that provides the needed methods :



```razor

@using BrowserInterop.Extensions

@using BrowserInterop.Geolocation

```



Now get the Geolocation API wrapper :



```razor

@code{

    private WindowNavigatorGeolocation geolocationWrapper;

    private GeolocationResult currentPosition;

    private List positioHistory = new List();

    protected override async Task OnInitializedAsync(){

        var window = await jsRuntime.Window();

        var navigator = await window.Navigator();

        geolocationWrapper = navigator.Geolocation;

    }

}

```



Note: For improving performance, you could also do it in your Program.cs file, there is no point in doing this for every page load.



Now you can get the current position with this 



```razor

<button type="button" @onclick="GetGeolocation">Get Current Position</button>



@if(currentPosition != null){

   
Current position : 

        <ul>

           
Latitude : @currentPosition.Coords.Latitude

            <li>Longitude : @currentPosition.Coords.Longitude </li>

            <li>Altitude : @currentPosition.Coords.Altitude </li>

            <li>Accuracy : @currentPosition.Coords.Accuracy </li>

            <li>Altitude Accuracy : @currentPosition.Coords.AltitudeAccuracy </li>

            <li>Heading : @currentPosition.Coords.Heading </li>

            <li>Speed : @currentPosition.Coords.Speed </li>

        </ul>

    </div>

}

//...

@code{

    //...

    public async Task GetGeolocation()

    {

        currentPosition = (await geolocationWrapper.GetCurrentPosition(new PositionOptions()

        {

            EnableHighAccuracy = true,

            MaximumAgeTimeSpan = TimeSpan.FromHours(1),

            TimeoutTimeSpan = TimeSpan.FromMinutes(1)

        })).Location;

    }

    //...

}

```

- If this is the first time, the browser will ask the user for permission and the method will return once the user gave his/her permission.

- If the user refuses, Location will be null, and you can get details in the Error property.

- All the options and return result are from the browser API, there is no magic here but the JSInterop plumbing.



## Watching position change



Browser also provides an API for watching GPS position change for mobile users. You can watch those changes in your Blazor app with BrowserInterop :



```razor

WatchPosition">Watch position

//...

@code{

    //...

    private List positioHistory = new List();



    private IAsyncDisposable geopositionWatcher;



    public async Task WatchPosition(){

        geopositionWatcher = await geolocationWrapper.WatchPosition(async (p) =>

        {

            positioHistory.Add(p.Location);

            StateHasChanged();

        }

        );

    }

    //...

}



```

- WatchPosition returns an IAsyncDisposable, we'll see its role later

- You need to call StateHasChanged because you are in a callback (StateHasChanged is called automatically at the end of each UI event handler)

- The callback should return a ValueTask or be async, here the async will trigger a build warning because there is no await. The purpose of returning Task is if you want to execute async code inside this callback, you can.



Now you need to add some code for disposing the watcher or else your component will not be disposed by the GC and your code will still be called after your user went to another page.



First, add the @implements clause on the top of your razor file :



```razor

@implements IAsyncDisposable

```



Then you can add a button for stopping the watcher and dispose it :



```razor

<button type="button" @onclick="StopWatch">Stop watch</button>

//...

@code{

    //...

    public async Task StopWatch(){

        await geopositionWatcher.DisposeAsync();

        geopositionWatcher = null;

    }



    public async ValueTask DisposeAsync(){

        await StopWatch();

    }

    //...

}

```



- The button is not mandatory but the DisposeAsync is: Blazor will call it when your component is destroyed. This is mandatory like for any event listening in C# (or in any other language that I know of). __This is very important, don't forget it !!__



The full source code is available here : [https://github.com/RemiBou/remibou.github.io/tree/master/projects/RemiBou.BlogPost.Gps](https://github.com/RemiBou/remibou.github.io/tree/master/projects/RemiBou.BlogPost.Gps)



## Conclusion



In this blog post, we saw how you can get your user GPS coordinates without writing a single line of JS. I wrote this library so anyone could work with .NET in the browser without knowing a thing about JS. Don't hesitate to send feedback or ask for more API on the project GitHub page : [https://github.com/RemiBou/BrowserInterop](https://github.com/RemiBou/BrowserInterop)
