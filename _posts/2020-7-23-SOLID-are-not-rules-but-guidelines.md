---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Architecture, SOLID]
---

# SOLID are not rules but guidelines
_This article expose my opinion that I got from my own experience and taste please, do not feel offended if you do not agree with it. Off course I might be wrong, it happens everyday._

## What is SOLID ?
[SOLID](https://en.wikipedia.org/wiki/SOLID#:~:text=In%20object%2Doriented%20computer%20programming,the%20GRASP%20software%20design%20principles.) are a set of principles for Object Oriented programing that are supposed to help you build great software. By great I mean maintainable, bug free, fast ... SOLID is an acronym where every letter is one principle. From my experience, you shouldn't follow them blindly but think about it, understand it and find where the added value can be. In this article we'll go through these principles, I'll try to explain them and the added value I see.

### SRP
SRP stands for Single Responsibility principle, the common explanation is "A class should have only one reason to change"... While it seems like a nice idea : a class with 2k lines is really hard to maintain, what does "one reason to change" mean ? Let's take this very simple class
```cs
public class Foo{
    public int GetResult(){
        return 1;
    }
}
```
It has many reason to change 
- We want to take more things into account when we compute the result (add parameters)
- We want to change the result , its value or even its type
- We want a GetResults that would return the history of result

And this is a class with 1 method with 1 line of code. In the SRP definition what bother me is the word "responsibility" its definition is really subjective and vaggue. I could have a single class of 100k lines of code whose responsibility is "provide online booking for hotel" and a class of 2 methods whose responsibilities are "return the price" and "save the price" and with these 2 responsibilities it wouldn't respect SRP. 

The definition of this principle is so vague that it can't be called a principle. It's nearly impossible to explain it to someone, every time the auditor will say "what do you mean by 'responsibility' or 'reason to change' ?". If you are an extremist of this rule you might endup with thousand of very little classes and from my experience it's way worse to maintain than large ones. 

__What should I keep from SRP ?__

SRP means that a class shouldn't do too many different things. Here are some examples :
- Manage the state of a Booking and dealing with basic email API => Add a Facade that will enable this class to abstract the email sending. Or better use events so it doesn't even know some email are sent
- Create HTML and SQL queries => create a class for the HTML, one for the SQL query and one that will make them work well together (some sort of MVC)
- Authenticate and Authorize => create a class for each role

And you can also use metrics of your own to help detect places where you could be breaking SRP :
- Line of code count
- Method count
- Field count
...

## OCP
OCP means "Open-Closed Principle", the usual explanation is "a class should be close to modification but opened to extension". What this means is that a class behavior should be changed not by changing its code source but by extending it (like implementing the same interface). First this kind of contradict SRP, because now there isn't a single reason to change a class. And then it's absolutely useless. Let's take this class

```cs
public interface IFoo{
    int GetResult();
}
public class Foo : IFoo{
    public virtual int GetResult(){
        return 1;
    }
}
```

From OCP if I want to change result (like divide it by 2) I would have to implement the decorator pattern like this

```cs
public class FooDivideBy2 : IFoo{
    private IFoo decorated;
    public FooDivideBy2(IFoo decorated){
        this.decorated = decorated;
    }
    public virtual int GetResult(){
        return decorated.GetResult() / 2 ;
    }
}
```

Instead of 1 class with 5 lines of code, I now have 2 class, one interface and 17 line of code. More than 3 times more line of code, if you apply this principle to larger code base with more interface, more implementation, the impact on maintainability is really important. 

One more thing : let's say I now want to return 2 instead of 1, why would I create a new class when I can change the existing one ? 

__What should I keep from OCP ?__

When you create a library (public or internal to your large company) by definition your class are closed : the user can't (easily) change their content. So you need to think about it and leave some extension hook so your user can customize your library. In other situation forget about it :)

## LSP
LSP stands for "Liskov substitution principle", its usual explanation is "an object can be replaced by any instance of its subtype without altering the corectness of the program". This means that, from the example from before, we could use FooDivideBy2 or Foo everytime we need an IFoo and the program would still be correct. What I understand is that when you use an interface you shouldn't worry about the implementation that will be used.

This principle is really nice and going against it adds "leaky abstraction". This is the case for the IQueryable interface in .NET : when you use it you have to know the implementation your are using because they don't all behave the same.

I see 2 problems with this principle :
- Some interface/abstract class are used to provide a common entry point and the interface definition doesn't say anything about the what it should do, like IObservable
- Not all implementation of an interface shares the same edge cases and error. For instance this interface :

```cs
public class IDataSave{
    void Save(Data data);
}
``` 
If I implement it by saving the data in a json file I will need some specific things done on the Data class (like public getter and setter or parameterless constructor) or it would fail with an IOException if there is some problem on the file system or it would fail because some fields are recursive ... These are implementation specific details that the user might need to know.

__What should I keep from LSP ?__
You should aim at respecting LSP : the user of the interface should ignore the implementation details and should use the interface the same way for every implementations. But you need to know that in some case it's impossible or very expensive to do.

## ISP
ISP stands for "Interface Segregation Principle". Saying it's better to have many specific interface than one general purpose.

I don't see any problem with this principle, and I would say that if you end up with this interface

```cs
public interface IBookingManagement{
    void SaveBooking(Booking aBooking);
    void DeleteBooking(int aBookingId);
    void ValidateBooking(int aBookingId);
    void DenyBooking(int aBookingId);
}
```

You didn't need an interface in the first place ! Interface does not change anything to coupling or code quality, they reduce code readability. So if you want to use some, make sure they bring value. When is that ? I see 2 cases :

- You have multiple implementations of this interface and the choosen implementation will change at runtime
- For testing purpose. I always put things that are out of my control behind an interface so I can mock/fake them for my tests : clock, random, external API, OS dependencies ...

__What should I keep from ISP ?__

Almost everything, if one interface has many method and they are not all used in the same context, then you might not need an interface or it's doing too much and you should split it.

## DIP

DIP stands for "Dependency Inversion Principle". The idea is there shouldn't be dependencies between implementations but between an implementation and an abstraction layer (interface). 

As I said before interface don't change a dime in coupling. 2 classes can be decoupled while still having a dependency between the two of them, you just need to make sure they ignore they internal behavior and don't change how they call each other based on that knowledge. It's a fallacy to believe that by using the "introduce interface" refactoring you reduce coupling between your classes. Let's see this example

```cs
public class Foo{
    private IBar _bar;//injected somehow
    public void DoStuff(){
        _bar.SaveData("a;b;c;d")
    }
}
public interface IBar{
    void SaveData(string str);
}
public class Bar : IBar{
    public void SaveData(string str){
        if(str?.Length > 30){
            throw new InvalidOperationException(str);
        }
        var data = str.Split(';');
        //insert data in DB
    }
}
```

Here having an interface doesn't change anything to the coupling between Foo and Bar : if we change the expected string format in Bar we'll have to change Foo. What you need to achieve is to reduce coupling by thinking about your class interface (By "interface" I mean all its public methods, not the C# "interface" it implements).

__What should I keep from DIP ?__

Design your modules so they don't know how its dependencies works appart from what is specified in the public method signature and documentation. 

## When you drink the SOLID kool-aid

I will tell you a bit of my story and I think I'm not the only one with this kind of experience. I was tech lead / architect / mentor / ... at a small software editor (under 20 devs at that time) when I started to read about SOLID. And I got into it, I thought it was the answer to all our maintainance problem, spaghetti / lasagna / calzone code base, countless bug etc ... So I tried to teach it to the team and following the principles on my own work. But it didn't change anything :

  - My code was hard to read for the rest of the team
  - The rest of the team didn't understand a single thing about the rules and never tried to respect it. 

From this experience I learned a few things :

  - Only teach pragmatic thing to your team(s). The dogma can help for finding problems but it's never a solution.
  - If you find some rules that seems perfect : try to find their limit, make an personal opinion out of it
  - Never think that sending a link to a good practice article to your team will change anything
  - Evaluate migration, plan and keep migrating until it's 100% done. No it won't work if you apply it "only o new development". In 5 years if your code base is large enough, 90% will stay untouched yet it still need maintenance/optimization.
  - When you sell something to your team find some concrete example and propose detailed rules.

## Some rules that I prefer

Now I will tell you about a few rules that I prefer to apply to my own work because they don't make thing more complicated, they are easier to understand and put in practice (IMO) :

### Law of Demeter

The law of Demeter is quite easy to explain : in a method Do in a class Foo you can only do the following :
- change state of Foo (private fields)
- call a method on any parameter of Do
- call a method on any variable instanciated inside Do
- call a method on any fields of Foo

To apply this in C# it's quite easy : stop making properties setter public !

Instead of this :
```cs
public class Foo{
    public void Do(Bar b){
        b.Enabled = true;
    }
}
public class Bar{
    public bool Enabled{get;set;}
}
```
Do this :
```cs
public class Foo{
    public void Do(Bar b){
        b.Enable();
    }
}
public class Bar{
    //the coule be fully private
    public bool Enabled{get; private set;}
    public void Enable(){
        this.Enabled = true;
    }
}
```
Like this your are doing beautiful OOP : each object is responsible for its state. Class are not only data holder, they also bring behavior. And you will see with time that it's easier to maintain as everything will be more explicit : in the first example the verb "Enable" was never written. Here it's a simple example, but having every state change expressed as a verb make thing clearer and more obvious.

### YAGNI

YAGNI stands for "You Ain't Gonna Need It". What this means is that you should only do the work you are asked to do. Don't lay the ground for some future imaginary changes, or even code hidden feature so you can feel like a hero saying "it's already done" in a meeting.

Let's take an example : you are asked to create an CRUD app that will save data on MSSQL. What you do is you create 5 projects in Visual Studio, one for UI, one for Business, one for DAO and 2 for abstractions between each layer. Settings up the project, resolving dependencies, mapping object between layers ... cost you like 40% of the project. This work can be valuable in an imaginary future indeed but you paid for it and you are not sure it'll be useful. 

It's like choosing to buy a family car when you are in your 20s because you think you will have kids. What will happen ? You might have kids when you will be 30 and by that time your car will be obsolete or broken, so you buy a new one. 

You can prepare for future evolution only if it doesn't increase your project cost. You. don't. know. the. future.

### Make the implicit explicit

This is a rule I love to repeat in my head when coding. Let's say you have 2 piece of code

```cs
public class Foo{
    // return -1 if value is not valid 
    public int Validate(int id){
        if(id > 10000)
            return -1;
        return 0;
    }
}
```
A caller must read the documentation and then compare the result to -1 to see if the value is correct or not. This is not complicated but still I prefer to do it like this

```cs
public class Foo{
    public bool IsValid(int id){
        if(id > 10000)
            return false;
        return true;
    }
}
``` 
We don't need any documentation, the behavior is explicit, we'll integrate this method way faster than the previous one. 

A good sign is indeed documentation : if you need to read a method documentation it might not be explicit enough. If you can't make it more explicit without impacting readbility (like changing to a 200 char long method name) then add comment.

### KISS

KISS stand for "Keep It Simple Stupid". It's not a rule and it's very subjective, so it's easy to have argument with a colleague about wether something is simple or not. But for me, KISS also means "Don't follow rules, follow the value", let's copy and example for a previous paragraph :

```cs
public class Foo{
    private IBar _bar;//injected somehow
    public void DoStuff(){
        _bar.SaveData("a;b;c;d")
    }
}
public interface IBar{
    void SaveData(string str);
}
public class Bar : IBar{
    public void SaveData(string str){
        if(str?.Length > 30){
            throw new InvalidOperationException(str);
        }
        var data = str.Split(';');
        //insert data in DB
    }
}
```

What is the value of IBar if I have only one implementation ? None. But it costs a lot :
- Every single time I will navigate between Foo and Bar, I will endup on the interface
- Every time I want to add a parameter to SaveData I will have to change the class and the interface
- I will have to setup DI for injecting Bar
...

If there is no added value, remove the superflux. If it's simple to insert data directly in your controller DO IT, ignore SOLID, ignore everything, do what seems to be simple.

It's the same for layered architecture, for each layer you need to evaluate the cost in complexity against its added value. Don't add layer becausesomeone said so. 

### Stop Renaming Shit (SRS, patent pending)

This is a rule of mine : even if he first naming was shit, keep it or rename it everywhere. It's really hard to follow a code base and see a concept like "product model" becoming "product type" on a layer which then becomes "item kind" on the next layer and then ends up "buyable thing category". Every developer has an opinion on how to name thing, don't be presemptous, you are not better than your colleague, keep its choice it'll be better for both of you.

## Conclusion

Our job is difficult as we have a lot of complexity to apprehend and sometimes some rules seems like the magic solution to all our problem : they are not and never will be. 

I hope I was able to explain my opinion clearly in this post, don't hesitate to question my skills in the comment :)