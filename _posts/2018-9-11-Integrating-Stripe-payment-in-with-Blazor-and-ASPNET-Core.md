---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor, Stripe, JSInterop]
---
# Integrating Stripe in a Blazor and ASPNET Core app
Now our app is finished, I need to make money with it. The first thing I thought about was creating sponsored content : you post something and in exchange we display it on top of the results. 

For the payment I chose [Stripe](https://stripe.com/fr) as it seams easy to integrate (there is a nuget package for all the server side work).

The purpose of this blog post is mainly to show how Blazor JS Interop works and how easy it is.

## Client Side

Stripe works that way : you call one of their JS method that opens up a modal asking payment details. When the payment details are validated by Stripe, you got a token back.

On the server side you call stripe with this token and the amount, and you're good to go.

First thing : the Blazor code for opening the modal. In my app it occurs on a button click :

```cs
@implements IStripeCallBack
//whatever
@function{
    protected async Task OnClick(UIEventArgs ev)
    {
       await JSRuntime.Current.InvokeAsync<string>("stripeCheckout",new DotNetObjectRef(stripeCallBack), amountInCts);
    }
    [JSInvokable]
    public async Task TokenReceived(string token)
    {       
       await GetCharge(token);
    }
}
```
- this is the component itself and it implements IStripeCallBack so I'll be able to reuse this code in an other component
- GetCharge is just a method that calls the server with the given token
- [JSInvokable] is mandatory if you want to call this method from JS
- I simplified the code for the sake of brievety, you can find the working project [here](https://github.com/RemiBou/Toss.Blazor/blob/master/Toss/Toss.Client/Pages/Home/NewToss.cshtml)
- we wrap the instance that'll be called by JS (this) in a DotNetObjectRef. If we don't do that the json serialized version of the object will be send to JS.

Now the JS part

```js

stripeCheckout = function (callBackInstance, amount) {
    var handler = StripeCheckout.configure({
        key: 'pk_test_IAEerhZ6JVmmcj9756zIZegI',
        image: 'https://stripe.com/img/documentation/checkout/marketplace.png',
        locale: 'auto',
        token: function (token) {
            callBackInstance.invokeMethodAsync('TokenReceived', token.id)
                .then(r => console.log(token));
        },
        currency: 'EUR'
    });
    // Open Checkout with further options:
    handler.open({
        name: 'Stripe.com',
        description: '2 widgets',
        zipCode: true,
        amount: amount
    });
    return Promise.resolve();
};
```
- "key" is the public key from stripe
- I return Promise.resolve() as every js interop must be asynchronous (not mandatory per Blazor documentation but I've seen bugs when I'm not doing it)
- "callBackInstance.invokeMethodAsync" is the JS interop call, this will call the method TokenReceived on my component instance

## Server Side

Now the server side is simple

```cs

    public class StripeClient : IStripeClient
    {
        private readonly HttpClient httpClient = new HttpClient();
        public StripeClient(string stripeSecretKey)
        {
            StripeConfiguration.SetApiKey(stripeSecretKey);
        }
        public async Task<bool> Charge(string token, int amount, string description, string email)
        {
            var chargeOptions = new StripeChargeCreateOptions()
            {
                Amount = amount,
                Currency = "eur",
                Description = description,
                SourceTokenOrExistingSourceId = token,
                ReceiptEmail = email
            };
            var chargeService = new StripeChargeService();
            StripeCharge charge = chargeService.Create(chargeOptions);
            return charge.FailureMessage != null && charge.Paid;
        }
    }
```
- the secret API key must reside in your secrets json  ([a little google search shows that some people are not aware of it](https://www.google.fr/search?q=site:github.com+StripeConfiguration.SetApiKey&newwindow=1&rlz=1C1GGRV_enFR752FR752&ei=zDiYW4r1N8zCgAbqgJzICA&start=10&sa=N&biw=1348&bih=612))
- I should send the amount from the client to the server, then compute the amount on the server side and if it doesn't match raise an exception so I'm sure that the amount displayed is the amount charged
- Use stripe nuget package it's 100 times easier than managing the HTTP request by hand (I tried it)

## Conclusion
Once again we can see that Blazor is quite easy to use with other tools / API. In my opinion the current framework (minus bugs) will be ready for production once the performance for client-side Blazor are fixed.

## References
- <https://stripe.com/docs/charges>
- <https://blazor.net/docs/javascript-interop.html#invoke-net-methods-from-javascript-functions>


