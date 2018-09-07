---
layout: post
title: Toss
feature-img: "assets/img/portfolio/github-icon.png"
img:  "assets/img/portfolio/github-icon.png"
date: February 2018
tags: [ASPNET Core, Blazor]
---

[![Build status](https://ci.appveyor.com/api/projects/status/mek4xbti51pog09j?svg=true)](https://ci.appveyor.com/project/RemiBou/toss-blazor)

# Toss.Blazor
Twitter-like web application using Blazor. You can login, post a new message (a "toss") with hashtag and select your favorite hashtags for finding messages.

# Tech stack
- Blazor (0.5.1)
- Bootstrap 4
- Toastr
- Asp.net Core 2.1.1
- MediatR
- CosmosDB
- WebDriver

# Feature list
- Security pages : auth/open id, edit account, reset password
- Push new messages to the home page
- Filter message by hashtag
- Add hashtag to your profiler
- (pending) pushing ads to the home page

# Running this sample
- checkout the project
- run CosmosDB Emulator
- Edit Toss.Server secrets with your values using the empty-secrets.json provided
- Build Toss.Server
- Run Toss.Server
- This sample uses mailjet as mail provider, edit MailService if you want to change that

