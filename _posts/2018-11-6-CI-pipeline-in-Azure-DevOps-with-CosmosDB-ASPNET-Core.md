---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Azure DevOps, ASPNET Core, Cosmos DB]
---

# Setup a CI pipeline in Azure DevOps (VSTS) with CosmosDB Emulator, ASPNET Core
In my project [Toss](https://github.com/RemiBou/Toss.Blazor) so far the challenge is more about integration with CosmosDB than complex business logic implementation so my testing strategy is more high level : integration and E2E. There is a challenge with this kind of test when you want to execute them from your CI pipeline as you must setup the whole environment without direct access to the system.

The service I'll be using for this is Azure DevOps, formerly known as Visual Studio Team Service. This tool changed a lot this past month as now we can setup the whole pipeline using yml file stored in our source control. In this blog post we'll see how to setup all the element together.

## Setup pipeline

This step is rather easy but still mandatory.

For setting up a pipeline click on the + next to your project name and click on "New build pipeline". Here you'll have to choose your repo and choose a template so Azure DevOps will check it out (yeah) and read the yml definition and execute the pipeline.

This pipeline is made of multiple steps which are things executed by the computer (agent) running the pipeline, there is a lot of built-in tasks, you can find some of them here : <https://docs.microsoft.com/en-us/azure/devops/pipelines/tasks/index?view=vsts>.

## Choosing the VM running the pipeline

The first thing to do is to choose the right VM for running the pipeline. Here I chose the Visual Studio on Windows Server because I know that the CosmosDB Emulator runs only on windows (I think they are working on a Linux / MacOS version).

The first line of your yml fille will be

```yml
pool:
  vmImage: 'vs2017-win2016'
```

- All the available images are here : <https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=vsts&tabs=yaml#use-a-microsoft-hosted-agent>
- Good to know : this VM will be created from scratch for every run of your pipeline, which cause your build process to be slow, but you are sure there is no side effect between them.

## Setup SDK

Next step is to start creating your build steps. The first one is checking that the right version of the .NET Core SDK is setup. There is a task for this :

```yml
variables:
  buildConfiguration: 'Release'

steps:
- task: DotNetCoreInstaller@0
  displayName: 'Install SDK 2.1.403'
  inputs:
    version: 2.1.403
```

- For finding the task name I use the GUI editor which has a tool for seing the YML source corresponding to a step.
- All the SDK versions are available here <https://github.com/dotnet/core/blob/master/release-notes/releases.csv>

## Build project

Now we need to build our project, which is not very hard

```yml
- script: dotnet build --configuration $(buildConfiguration)
  displayName: 'dotnet build'
 ```
 
 - buildConfiguration refers to the variable defined on the previous part. I could have hard coded it

## Start CosmosDB Emulator

For my integration test I need access to a CosmosDB server. For this I could have used a paying COsmosDB instance, but I'm cheap so I prefer to use CosmosDB Emulator.
Before setting up the step you need to install the emulator on your Azure Devops account here : <https://marketplace.visualstudio.com/items?itemName=azure-cosmosdb.emulator-public-preview>

Now the step

```yml
- task: azure-cosmosdb.emulator-public-preview.run-cosmosdbemulatorcontainer.CosmosDbEmulator@2
  displayName: 'Run Azure Cosmos DB Emulator container'
```

- For finding the task name I used the GUI designer once again, because this is not documented anywhere.
- There is no argument
- This runs CosmosDB Emulator on a container, so we'll need to find a way to pass the container host name to our test, this is the next step.

## Run integration tests

My integration tests are xUnit tests. Here is my yml configuration for this step :

```yml
- script: dotnet test Toss.Tests\Toss.Tests.csproj  -v n
  displayName: 'Tests'
  env: { 'CosmosDBEndpoint': "$(CosmosDbEmulator.Endpoint)" }  # list of environment variables to add
```

- I force the project on dotnet test so it doesn't scan the full repo searching for test assembly (dotnet test output is already a nightmare, if we can reduce it a bit it's not bad)
- "-v n" is for debugging purpose, I needed to Console.WriteLine a few things
- the environement variable setting is for passing the CosmosDB host name to the test
- $(CosmosDbEmulator.Endpoint) is set by the previous step

I read the environment variable like this :

```cs
var dict = new Dictionary<string, string>
  {
       { "GoogleClientId", ""},
       { "GoogleClientSecret", ""},
       { "MailJetApiKey", ""},
       { "MailJetApiSecret", ""},
       { "MailJetSender", ""},
       { "CosmosDBEndpoint", "https://localhost:8081"},
       { "CosmosDBKey", "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="},
       { "StripeSecretKey", ""},
      {"test","true" },
      {"dataBaseName",DataBaseName }
  };

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(dict)
    .AddEnvironmentVariables()
    .Build();
 var startup = new Startup(config);
```
- The environment variable will override the localhost setting if it's set, so the test will run on my dev machine and on the agent.
- The CosmosDBKey for the emulator is always the same, lucky us :)

## Results

You can see my tests running here : <https://dev.azure.com/remibou/toss/_build/results?buildId=23&view=logs> (I need to fix the E2E tests, it'll be for an other log post).

## Conclusion

It's not very hard to setup, but as always with code oriented configuration, the discovery is hard if there is not enough tools / documentation. Here I had to find the task names using the GUI editor, hopefully a new VS addin will connect Azure DevOps account and yml editor for providing intellisens (in 10 years).

You can find the full yml here : <https://github.com/RemiBou/Toss.Blazor/blob/master/azure-pipelines.yml>

## Reference
- <https://docs.microsoft.com/en-us/azure/devops/pipelines/targets/cosmos-db?view=vsts>
- <https://docs.microsoft.com/en-us/azure/devops/pipelines/yaml-schema?view=vsts&tabs=schema>
- <https://weblog.west-wind.com/posts/2018/Feb/18/Accessing-Configuration-in-NET-Core-Test-Projects>
- <https://github.com/dotnet/core/blob/master/release-notes/releases.csv>
