# The story on why I left CosmosDB for RavenDB
CosmosDB is the cloud only NoSQL database from Microsoft hosted on Azure. For my project [Toss](https://github.com/RemiBou/Toss.Blazor) I chose it as the data store. Here is why :
- Fun : I didn't want to do RDBMS on my pet project because I do this 38hrs a week (thank you France :) )
- The scale it enables is certainly a lot above the scale I would need for this project 
- Azure : I wanted to do a Azure-first project: using only PaaS offering.

A few weeks ago I started to need more advanced usage than CRUD from CosmosDB and I chose to migrate everything to RavenDB. In this blog post I'll try to explain why.

Disclaimer : the purpose of this blog post is not to criticize CosmosDB, just to explain my choice. I, by no means, declare that your choice for CosmosDB was a bad decision. This decision was based on my experience, my taste and my guts.


## Lock-in
The first argument against CosmoDB is customer lockin : you can use CosmosDB only as a PaaS service on Azure paying the price they decided,
- If they increase it ? You pay more (honestly I've never seen Azure incerasing any price, they decrease it often). 
- If they decide to deprecate it on April 15th, you have to migrate 
- Beause it's deployed only on Azure, only a small subset of .net developer are working with it so finding tools and help is really hard. 

I changed my mind about creating an Azure-first app so this started to become a big deal for me. 

RavenDB on the over hand is still a document database but it's available for every OS and it's also available as a Docker image. Still there is the licensing problem, RavenDB 4.0 Community edition is free of charge but is licensed under AGPLv3, so I had to check and understand the [license](https://github.com/ravendb/ravendb/blob/v4.1/LICENSE) . Here is my understanding, please correct me if I'm wrong :
- AGPL is like GPL but also applies to software you use over the network (GPL applies only to libs you would ship with your application).
- If I change RavenDB Server code then you have to make it open source 
- I can use the driver with the current project MIT license as the client is lcensed under MIT.

## SDK
Because CosmosDB is a Microsoft product we can expect a good SDK. I was a bit disapointed here. First you can't use 

## Features

## Local / CI experience

## Migration
