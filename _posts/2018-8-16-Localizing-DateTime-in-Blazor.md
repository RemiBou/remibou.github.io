# Localizing DateTime (and numbers) in Blazor

When we create an used by people from many country we soon face the challenge to translate it ([see my previous article](https://remibou.github.io/I18n-with-Blazor-and-ASPNET-Core/)). An other challenge is to make non-string data (numbers, date) understandable for everyone. For "01/02/2018" means "February 1st 2018" for a french guy but it means "January 2nd 2018" for an english guy.

Fortunatly all these specific format are already setup by Microsoft, when we call "DateTime.Now.ToShortDateString()" it looks for the current culture (stored in the static property "CultureInfo.CurrentCulture") and create the good string for representinf the DateTime.

This is the best argument for Blazor : you can use all the existing .net library / API. You can if it respect 2 conditions :
- it's in a library that targets netstandard 
- the part of the standard it uses are implemented by the mono team in the web assembly implementation ([repo github](https://github.com/mono/mono)).

The CultureInfo and the other class used for formating are part of netstandard (I guess it's defined [here](https://github.com/dotnet/standard/blob/master/netstandard/ref/System.Globalization.cs) ) and is implemented by the mono team.

## Getting the user language

The first step is to get the user language. Because it's set in the browser and sent in every httprequest in the accept-language header, it should be available in js. 
