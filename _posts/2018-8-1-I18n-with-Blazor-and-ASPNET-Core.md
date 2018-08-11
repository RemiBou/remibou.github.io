# Internationalizing a Blazor App with ASPNET Core as backend service
A common task for developers is to make their aplication translated into the users language. Most of the frameworks provide tools for enabling easily such a task and et the developer focus on other things. Blazor being a new framework there isn't such a thing. In this blog post I'll show you my way of doing it.

## Server side
The translation willbe stored server side and the client will request for all the tranlations in its language. I do this for avoiding downloading all the translations in all the languages on the client side. My server side is an APSNET Core 2.1 app so I'll use the existing feature for managing those.

### Ressource file
All the translated content is stored on ressources files which will be embeded on the assemblies once deployed. You need to create a Resources folder and inside add a ressource file called "Client.fr.resx" : 
 - translations are grouped by class to translate so here we'll use a dummy class called "Client" for all the translatiosn hapenning on the client
 - "fr" is the 2 letter code of the culture I want to translate
 
Visual Studio gives a table editor for this kind of files but the xml content is pretty straighforward :

```xml
```
