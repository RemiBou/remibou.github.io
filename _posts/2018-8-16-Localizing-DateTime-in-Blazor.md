# Localizing DateTime (and numbers) in Blazor

When we create an used by people from many country we soon face the challenge to translate it ([see my previous article](https://remibou.github.io/I18n-with-Blazor-and-ASPNET-Core/)). An other challenge is to make non-string data (numbers, date) understandable for everyone. For "01/02/2018" means "February 1st 2018" for a french guy but it means "January 2nd 2018" for an english guy.

Fortunatly all these specific format are already setup by Microsoft, when we call "DateTime.Now.ToShortDateString()" it looks for the current culture (stored in the static property "CultureInfo.CurrentCulture") and create the good string for representinf the DateTime.

This is the best argument for Blazor : you can use all the existing .net library / API. You can if it respect 2 conditions :
- it's in a library that targets netstandard 
- the part of the standard it uses are implemented by the mono team in the web assembly implementation ([repo github](https://github.com/mono/mono)).

The CultureInfo and the other class used for formating are part of netstandard (I guess it's defined [here](https://github.com/dotnet/standard/blob/master/netstandard/ref/System.Globalization.cs) ) and is implemented by the mono team.

## Getting the user language

The first step is to get the user language. Because it's set in the browser and sent in every httprequest in the accept-language header, it should be available in js. Indeed there is "navigator.languages" which is [compatible with all the lastest version of every browser](https://developer.mozilla.org/en-US/docs/Web/API/NavigatorLanguage/languages#Browser_compatibility).

The interop calls look like this

```js
navigatorLanguages = function () {
    return navigator.languages;
}
```

- I have to create a small function for this because js interop only accept function call, not properties.

The CS code for calling this is pretty simple

```cs
public static async Task<string[]> Languages()
{
    return await JSRuntime.Current.InvokeAsync<string[]>("navigatorLanguages");
}
```

## Setting the current language

CultureInfo.CurrentCulture is bound to thread so we can't use that as Blazor / monowasm might and will create multiple thread (for instance using await will certainly create a thread). See [this github issue](https://github.com/aspnet/Blazor/issues/1056) where we struggle to find the right place for initializin the language. I don't know how they do it with ASPNET Core to pass the set culture in all the current request thread but here we have the chance to be statefull so we can set it for the whole app using the property " System.Globalization.CultureInfo.DefaultThreadCurrentCulture" that will be used at thread create. For setting something for the whole app we can use the Main() method here is my code for setting it :

```cs
JSRuntime.Current.InvokeAsync<string[]>("navigatorLanguages")
    .ContinueWith(t => CultureInfo.DefaultThreadCurrentCulture = t.Result.Select(c => CultureInfo.GetCultureInfo(c)).FirstOrDefault())
    .ContinueWith(t => new BrowserRenderer(serviceProvider).AddComponent<App>("app")) ;
```

- I use ContinueWith because Main() is not async
- The app rendering is added after the language initialization otherwise we'd have some label with the bad culture
- I'm lucky JSInterop is available here :)

## Conclusion 

As I move forward with Blazor, most of the problem I encounter are due to lifecycle and Task management but resolving this kind of problem makes me undertstand better the whole thing, I hope you too (I even did a useless checkout of mono).

