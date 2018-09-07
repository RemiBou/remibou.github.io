---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [meta]
---

# This Blog

## Why am I doing this ?

So I created this blog for many reasons :
- Testing jekyll on github pages
- Work my writing skills
- Explain to the world what I am doing
I'll try to post as often as possible (every week seems a nice challenge).

## Who am I ?
My name is Rémi Bourgarel, born in 86 in Toulouse where I still live. I am passtionnated by software development I have currently many point of interest :
- .net :  .net core, .net framework, asp.net, blazor, ml.net
- Spring
- Angular (the last two are for my new job)
- Cloud computing (mainly azure for now)

## Jekyll

Jekyll is a nice static site generator. What this means is that it takes your content (here md files) and generate raw html pages from it. Github can manage the whole thing : file generation, generated page storage, web server hosting and domain name providing.

From a performance and even security point of view it's really interesting as there is no server side code execution (there is a bit of code for loading the file content and sending it to the client).

### How did I set it up ?
- I forked https://github.com/barryclark/jekyll-now 
- Changed the repo name to {my user name}.github.io
- Edited https://github.com/RemiBou/remibou.github.io/blob/master/_config.yml
- Edited a md file in https://github.com/RemiBou/remibou.github.io/blob/master/_posts/
- Browse https://remibou.github.io/

That's it. The only problem was that the site was not being generated because I had some syntax error in my config file (space missing after ":"). I received an email from github with the exact error message and after correcting it the site was up and running.

### Whats the point
As I said there is security and performance but there is no traffic on this site so it's not really something really interesting at this stage : 
- Simplicity of setup, around 5-10 min
- In github so I can edit my post on my local machine
- Working with markdown so the syntax is easy and I don't have to deal with some weird Wysiwyg (always have to spell this in my head to get it right)
- Working with git so I can do branching for WIP and have a full history
- Lot of nice plugins like disqus or google analytics and you configurate it with one line of config
- no hard to understand GUI like wordpress or shitty Wysiwyg like blogspot

## Blog post list
https://trello.com/b/DjyTFhPy/blog
