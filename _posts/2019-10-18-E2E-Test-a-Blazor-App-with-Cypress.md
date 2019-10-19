---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor,Tests,RavenDB,Cypress,AzureDevOps]
---

# How to do en-to-end tests with Cypress on a Blazor app using Docker Compose
On my [Toss project](https://github.com/RemiBou/Toss.Blazor), I chose to have some end-to-end (e2e). End-to-end test on web project are tests that automate a browsing session on a web browser. Most of the time it works by using API provided by an existing browser (like chrome). Those kind of tests have many drawbacks :
- Force you to add ids everywhere on your html code so you can find element on your test code
- Are often flaky because some load time might vary between two test run or you can change your front-end code without thinking about the changes needed in the test
- Are hard to build first because you can forget some steps, like scroll down or click here.

But they are important for making sure that your app works and you don't have a huge error when your app startup because you miss a js reference or something else.

At first I started my tests with Selenium. Mainly because it's the most common way to do this and because there was some example on the aspnet core repo. But Selenium test are hard to write and very flaky (just look at my [git logs](https://github.com/RemiBou/Toss.Blazor/commits/master)) for many reason :
- An element must be displayed on the screen for accepting interaction, so you must code the scrolling up/down, size screen etc ...
- You can only use the browser like a normal user so you can't add code for waiting the end of an http query or check the status of a xhr.
- There isn't much debugging information provided : screenshot are hard to get and videos impossible.

I heard a lot of good thing about [cypress](https://www.cypress.io/) so I decided to give it a shot after an unsuccessful battle against Selenium.

## Start the application and dependencies

Because my E2E test were done with Selenium it was a .net project, so I was able to launch RavenEB.Embedded. Cypress running on the browser runtime, I will get rid of this project, so I decided to use docker-compose. 
I really like docker and using docker-compose for running E2E tests seems like a good idea. For running my app in a docker compose I need to build a docker image for it, here it is :

```dockerfile
FROM mcr.microsoft.com/dotnet/core/runtime:2.2.7-alpine as runtime227

FROM mcr.microsoft.com/dotnet/core/sdk:3.0.100-alpine AS build
# import sdk from 2.2.7 because we need it for running ravendb embedded
COPY --from=runtime227 /usr/share/dotnet /usr/share/dotnet 
WORKDIR /src
COPY ./Toss.Client/Toss.Client.csproj ./Toss.Client/
COPY ./Toss.Server/Toss.Server.csproj ./Toss.Server/
COPY ./Toss.Shared/Toss.Shared.csproj ./Toss.Shared/
COPY ./Toss.Tests/Toss.Tests.csproj ./Toss.Tests/
COPY ./Toss.sln ./
RUN dotnet restore ./Toss.sln
COPY ./Toss.Client ./Toss.Client
COPY ./Toss.Server ./Toss.Server
COPY ./Toss.Shared ./Toss.Shared
COPY ./Toss.Tests ./Toss.Tests
RUN dotnet test ./Toss.Tests
RUN dotnet publish Toss.Server/Toss.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["dotnet", "Toss.Server.dll"]
```
 - We can see a really nice feature of Docker : I was able to import the 2.2.7 runtime into a 3.0 sdk docker image because my integration test runs RavenDB.Embedded which is not compatible with runtime 3.
 - With multi stage build I can use the sdk for building my app then only the runtime for executing it which will make my docker image smaller
 - I first copy the csproj and sln files for using layer caching : if I don't change anything to my csproj files, then the "RUN dotnet restore ./Toss.sln" line will use the cached version making my build faster.
 - "RUN dotnet test ./Toss.Tests" runs my integration tests, not the E2E. 

Then I need to create a docker-compose file describing my app and its dependencies :

```yml
version: '3.5'
services:
  web:   
    build: 
      context: .
    restart: always
    environment: 
      - GoogleClientId=AAA
      - GoogleClientSecret=AAA
      - MailJetApiKey=
      - MailJetApiSecret=
      - MailJetSender=
      - RavenDBEndpoint=http://ravendb:8080
      - RavenDBDataBase=Tests
      - StripeSecretKey=
      - test=true
    depends_on: 
      - ravendb    
    ports:
      - 80:80
  ravendb:
    image: ravendb/ravendb
```
- I expose the port 80 so I can access the app in my dev computer
- "test=true" tells my app to use fake dependencies for things like external service (mailjet, stripe) or non deterministic data (random, datetime.now)
- The good thing with docker compose is that they run on their own network with service accessible via their name, so I can run ravendb inside this network and it won't have an impact on the other ravendb I might be running on my dev computer.

Now I can run my app with the following command :

```bash
docker-compose up -d 
```

Maybe I'll use it as a starting point for hosting my app in production but it is not the point right now.

## Cypress Test

First I create the folder "Toss.Tests.E2E.Cypress" in my solution then cd on it. You need npm installed on your computer. Then type on your terminal :

```bash
npm init
npm install cypress
```

Once installed I got a cypress folder containing multiple folder, I cleaned up all the samples in the folder called "integration". My new test will be a js file inside this integration folder :

```js
/// <reference types="Cypress" />

describe('Toss Full Test', function () {
    let polyfill;
    const uuid = Cypress._.random(0, 1e6)

    before(() => {
        const polyfillUrl = 'https://unpkg.com/whatwg-fetch@3.0.0/dist/fetch.umd.js';
        cy.request(polyfillUrl).then(response => {
            polyfill = response.body;
        });
    });
    Cypress.on('window:before:load', win => {
        delete win.fetch;
        win.eval(polyfill);
    });
    const SubscribeEmail = "tosstests" + uuid + "@yopmail.com";
    const SubscribePassword = "tossTests123456!!";
    const SubscribeLogin = "tosstests" + uuid;

    it('Full process', function () {
        cy.server();
        //used for listenning to register api call and getting the recirction url from the http headers
        cy.route('POST', '/api/account/register').as('register');
        cy.route('POST', '/api/account/login').as('login');
        cy.route('POST', '/api/toss/create').as('create');
        cy.visit("/");

        disableCaptcha();

        //this could be long as ravendb is starting
        cy.get("#LinkLogin", { timeout: 20000 }).click();

        //register
        cy.get("#LinkRegister").click();
        cy.get("#NewEmail").type(SubscribeEmail);
        cy.get("#NewName").type(SubscribeLogin);
        cy.get("#NewPassword").type(SubscribePassword);
        cy.get("#NewConfirmPassword").type(SubscribePassword);
        cy.get("#BtnRegister").click();
        cy.wait('@register');
        cy.get('@register').then(function (xhr) {
            expect(xhr.status).to.eq(200);
            expect(xhr.response.headers['x-test-confirmationlink']).to.not.empty;
            cy.log("Redirect URL : " + xhr.response.headers['x-test-confirmationlink']);
            cy.visit(xhr.response.headers['x-test-confirmationlink']);
            disableCaptcha();
            //login
            cy.get("#UserName", { timeout: 20000 }).type(SubscribeEmail);
            cy.get("#Password").type(SubscribePassword);
            cy.get("#BtnLogin").click();
            cy.wait('@login');
            //publish toss
            cy.get("#LinkNewToss").click();
            var newTossContent = "lorem ipsum lorem ipsumlorem ipsum lorem ipsumlorem ipsum lorem ipsumlorem ipsum lorem ipsum #test";
            cy.get("#TxtNewToss").type(newTossContent);
            cy.get("#BtnNewToss").click();
            cy.wait('@create');

            //publish toss x2
            cy.get("#LinkNewToss").click();
            var newTossContent2 = " lorem ipsum lorem ipsumlorem ipsum lorem ipsumlorem ipsum  lorem ipsumlorem ipsum lorem ipsum #toto";
            cy.get("#TxtNewToss").type(newTossContent2);
            cy.get("#BtnNewToss").click();
            cy.wait('@create');


            //add new hashtag
            cy.get("#TxtAddHashTag").type("test");
            cy.get("#TxtAddHashTag").type("{enter}");
            cy.get("#BtnAddHashTag").click();
            cy.get(".toss-preview").first().click();
            cy.get(".toss-detail .toss-content").should("contain", newTossContent);

            // logout
            cy.get("#LinkAccount").click();

            cy.get("#BtnLogout").click();
            cy.url().should("eq", Cypress.config().baseUrl + "/");
        });
    })
})

function disableCaptcha() {
    cy.window()
        .then(win => {
            win.runCaptcha = new win.Function(['action'], 'return Promise.resolve(action)');
        });
}
```

- Cypress uses mocha for running the tests, so your test fixture must be a call to "describe()" function and inside each test there is multiple calls to a "it" function. As in every test runner, there are hooks for running things before/after each/every tests.
- Cypress is able to read every XHR request done by your site, but Blazor uses fetch for http call. Se need to remove the current implementation of fetch and replace it by a polyfill that uses xhr.
- We can see with the route() and wait() call how you can wait until an http request is done and how we can read its content : here I needed a way to get the confirmation link after a subscribtion, so server side I send this link in the http response header (only in test mode) and I read it on client side and then visit() the link.
- For E2E I prefer to write one big test that does a lot of thing, it's just a mater of taste. I don't practice TDD with E2E test it would be too hard, I prefer to create it when the development is done just for making sure it will keep working after my future updates.
- The method disableCaptcha is used for disabling recaptcha. Be careful here, I create the method with a "new win.Function" for a specific reason. When you do interop C# -> js, Microsoft.JSINterop (used by Blazor) [check that the argument send is a function](https://github.com/aspnet/Extensions/blob/master/src/JSInterop/Microsoft.JSInterop.JS/src/src/Microsoft.JSInterop.ts) with this code

```ts
if (result instanceof Function) {
    result = result.bind(lastSegmentValue);
    cachedJSFunctions[identifier] = result;
    return result;
} else {
    throw new Error(`The value '${resultIdentifier}' is not a function.`);
}
```

Here is the catch : on my test if I write "win.runCaptcha = function(action){return Promise.resolve(action);}" it would throw an error "The value 'window.runCaptcha' is not a function." because class definition (like Function) are namespaced by window so my method would be a function in the namespace of the cypress window, not on the namespace of the tested app window (it took me no less than 2 days to figure it out).

Now for testing this I cd on the test directory and enter

```bash
./node_modules/.bin/cypress open
```

Which opens a web UI where I can run my test and see them while they are executing. This GUI is really nice because you can go in the past and see the state of the UI or even browse the DOM. Now that my test passes I want to integrate cypress into my docker-compose so I can run it without installing cypress, which will help when I'll run it in my CI environment.

## Integrate Cypress into the docker compose

I added the following service to my docker-compose.yml

```yml
  cypress:
    image: cypress/included:3.4.1
    depends_on:
      - web
    environment:
      - CYPRESS_baseUrl=http://web
    working_dir: /e2e
    volumes:
      - ./Toss.Tests.E2E.Cypress/:/e2e
```

- I mount the cypress tests as a volume, so I can also get back the cypress test execution artifacts (screenshot and videos)
- The "depends_on" is really usefull for starting things in order (ravendb -> web -> cypress)
- By default cypress tries to test http://localhost, here I change it for the service url with the env variable CYPRESS_baseUrl

Then for running the test I run the following command from the root fo my project

```bash
docker-compose up --renew-anon-volumes --exit-code-from cypress --build
```

- "--renew-anon-volumes" is used for cleaning ravendb volumes before each run, so I start with an empty DB.
- "--exit-code-from cypress" is used for telling docker-compose to kill all the other services when the service "cypress" is done and it'll use the cypress return code as its own.
- "--build" will build our service with build specification (only "web")

## Integration on Azure DevOps

Now that I got a fully running integration I can replace the existing one for this. Because it uses docker-compose it'll be easier to maintain (I can test it on my dev machine and on multiple OS) and will also help for running other servcies. Here is my new "azure-pipelines.yml" file

```yml
pool:
  name: Azure Pipelines
  vmImage: ubuntu-16.04

steps:
- task: DockerCompose@0
  displayName: 'Run a Docker Compose command'
  inputs:
    containerregistrytype: 'Container Registry'
    dockerRegistryEndpoint: 'dockerhub remibou'
    dockerComposeFile: 'docker-compose.yml'
    dockerComposeCommand: 'up --renew-anon-volumes --exit-code-from cypress --build'

- task: PublishPipelineArtifact@1
  displayName: 'Publish Pipeline Artifact'
  inputs:
    targetPath: Toss.Tests.E2E.Cypress/cypress/videos/
    artifact: 'Cypress videos'
  condition: succeededOrFailed()
```

- I use an ubuntu image because it's lighter than windows and I don't think I'll run my app on windows server so it's better to do my build and test on a linux OS.
- You need to create a docker endpoint on Azure DevOps so it can login into docker hub for pulling public images
- I publish the cypress video as artifact so I won't have any problem for debugging it when it fails

## Conclusion

I hope this new test suite will make my E2E tests less flaky and more enjoyable to maintain and improve. 

I can also say that Cypress is waaaay better than Selenium in every possible way : 
- installation : a single docker service or an npm package
- development : no need to add wait everywhere and you can watch XHR requests
- execution : a web UI or a single command line
- debug. : on the web UI you can browse all the test execution and you have a video of the whole thing.