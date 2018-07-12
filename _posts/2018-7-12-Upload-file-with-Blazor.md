# Uploading a file in a Blazor app
Blazor is a SPA framework. At some point in a web application, you'll need to upload file to the server. Here is how you can do it with Blazor.

Right now, Blazor doesn't suport this out of the box, we'll have to load the file content with js, take this content in C# and send it to the server.

## Blazor side
You need a file input and handle the onchange event with Blazor/C#   

```html
<input type="file" onchange="@UploadFile" id="fileUpload" />
 async Task UploadFile()
    {
        var data = await JsInterop.GetFileData("fileUpload");
        var response = await Http.PostAsync("/api/upload",new ByteArrayContent(Convert.FromBase64String(data)));
        var fileTempName = await response.Content.ReadAsStringAsync();
    }
```

- We'll receive the file binary as base64 from the javascript function because byte arrays are not serializable in json and this is the format of exchange between C# and js.
- the api will just send back the file name where the file is saved, but you can do what you want.

## JS side
Here is the method for getting the file content

```js
const readUploadedFileAsText = (inputFile) => {
    const temporaryFileReader = new FileReader();
    return new Promise((resolve, reject) => {
        temporaryFileReader.onerror = () => {
            temporaryFileReader.abort();
            reject(new DOMException("Problem parsing input file."));
        };
        temporaryFileReader.addEventListener("load", function () {
            var data = {
                content: temporaryFileReader.result.split(',')[1]
            };
            resolve(data);
        }, false);
        temporaryFileReader.readAsDataURL(inputFile.files[0]);
    });
};
Blazor.registerFunction("getFileData", function (inputFile) {
    var expr = "#" + inputFile.replace(/"/g, '');
    return readUploadedFileAsText($(expr)[0]);
});
```

- I have to do this weird replace as there is a bug in Blazor regarding async js interop calls (instead of passing "inputFile" it passes ""inputfile""))
- I use a promise as we can't read file synchronously in js and Blazor needs a Promise for calling async js method
- This code is greatly inspired by <https://blog.shovonhasan.com/using-promises-with-filereader/>, it helped because I find it very hard to understand promise.
- we could easily remove the jquery dependency
- readAsDataURL appends information to the file base64, so we have to split it and get the 2nd part

### Blazor JS Interop
Now the bridge between C# and JS

```cs
 public static async Task<string> GetFileData(string fileInputRef)
        {
            return (await RegisteredFunction.InvokeAsync<StringHolder>("getFileData", fileInputRef)).Content;
        }
```

- I had to create a simple StringHolder because js interop does not (yet) support string exchange

### File saving on server

Here is the code used to save the file content on the server, but once you have the binary, you could do what you want with it

```cs
 [Authorize, Route("api/upload")]
    public class UploadController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Save()
        {
            var tempFileName = Path.GetTempFileName();
            using (var writer = System.IO.File.OpenWrite(tempFileName))
            {
                await Request.Body.CopyToAsync(writer);
            }
            return Ok(Path.GetFileNameWithoutExtension(tempFileName));
        }
    }
```

- I like to use the Path API for managing this kind of files 
- It's sad we can't get the binaries as a method parameter (I hate those Request/Response mega-class properties)

You can find this code and execute it on my Toss project here <https://github.com/RemiBou/Toss.Blazor>.

### Reference
- <https://blog.shovonhasan.com/using-promises-with-filereader/>
- <https://blazor.net/docs/javascript-interop.html>
- <https://github.com/aspnet/Blazor/issues/527>
- <https://github.com/aspnet/Blazor/issues/479>
- <https://stackoverflow.com/questions/32556664/getting-byte-array-through-input-type-file/32556944#32556944>
