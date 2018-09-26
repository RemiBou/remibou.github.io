---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [meta]
---

# Moving from Unit Test + Mock to Integration test and fake
On my Toss project I decided to move from a Unit Test approach to an Integration test approach. i'll try to explain the reasons and the technique employed in this blog post.

## How I build my Unit Test

I build my unit test this way : a method (unit) has input (parameters and dependencies) and output (return, dependencies and exceptions). 

A unit test will "arrange" the input :
- the parameters are forced
- the dependencies are setup via a mocking framework (Setup() in Moq)
And "assert" the output :
- the return and exceptions are checked
- the dependencies are validated via the mocking framework (Verify() in Moq)

In theory this is perfect : 
- only my code is executed at test assert, I don't depend on other systems
- if one test fails, the reason will be easy to find
- every piece of code will be writen following the red/green (create test and add code until tests passes)

## Problems with Unit Tests

This approach has drawbacks that lead me to getting rid of it, the drawbacks are the following :
- Because of the mandatory mocking/faking of dependencies, you are not testing an interface but an implementation. And a method is the lower block of implementation. Every time I'll do a small change in my implementation (for instance I change a loop on "SaveOnItem" for a call to "SaveAll") I will need to change my tests.
- Setting up everything is a lot of code, look at [this file](https://github.com/RemiBou/Toss.Blazor/blob/46c2440b4a57e224464d5f6f61cd8b302f54aa47/Toss.E2ETest/Infrastructure/MockHelpers.cs) I had to create to mock the ASPNET Core Identity dependencies. If I have the simple formula "1 LoC  = X bugs", we can say that I will spend more time debugging my tests (and that's what happened) than my actual code !
- Because you are not testing everything altogether you can have

## Technical solution

## New problems

## Reference
