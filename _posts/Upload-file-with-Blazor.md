# Uploading a file in a Blazor app
Blazor is a SPA framework. At some point in a web application, you'll need to upload file to the server. Here is how you can do it with Blazor.

Right now, Blazor doesn't suport this out of the box, we'll have to 
1. Load the file content in JS
2. Find a way to call a C# method when the file content is loaded
3. Send the content to the backend 
4. save the content on a file and give back the file's url

## Getting the file content
Getting a file content in js is not very difficult, here is my html input

```html
<input type="file" data-image-upload="123" />
```

here is the jQuery

```js

$(document).on('change', 'input[type=file][data-image-upload]',function () {
    console.log("Loading file");
    var reader = new FileReader();
    reader.onload = async function () {
        var arrayBuffer = this.result,
            array = new Uint8Array(arrayBuffer);
        console.log("File Loaded");
        var url = await Blazor.invokeDotNetMethodAsync({
            type: {
                assembly: 'Toss.Client',
                name: 'Toss.Client.Services.JsInterop'
            },
            method: {
                name: 'Upload'
            }
        }, btoa(array));
    };
    reader.readAsArrayBuffer(this.files[0]);
});

```
 - I get the file content with readAsArrayBuffer
 - I convert thefile content to base64 with btoa(), you have to use a serializable type as parameter
 - the sending method is async, so I have to use the async/await syntax.
 
 ## Sending file content to backend API
 
 Here is the method called by Javascript in the previous chapter
 
 ```cs
 public class JsInterop{
        public static async Task<string> Upload(string base64fileContent)
        {
            ConsoleLog("C# just received byte[] : " + base64fileContent.Length);
            var data = Convert.FromBase64String(base64fileContent);
            var httpClient = (HttpClient)Program.serviceProvider.GetService(typeof(HttpClient));
            var response = await httpClient.PostAsync( "/api/upload",new ByteArrayContent(data))
            return await response.Content.ReadAsStringAsync();
        }
}
 ```
 
 - I get the binary corresponding to the base64 string
 - I excpect the file url back from the server
 
 ## Receiving content and saving file
