---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [ASPNET Core, Blazor, JSInterop, Cypress, Tests]
---

# Test your JSInterop code with cypress.io

[Cypress.io](https://www.cypress.io/) is, for me, a game changer in the world of web E2E test dominated by WebDriver based framework :
- It's easy to setup 
- it's easy to integrate into a CI pipeline
- The API are fine (I still don't like the assertion methods)
- The debug information it provides are golden
- There is a lot of methods for making your tests less flaky (you do'nt have to add random wait every 2 lines)

## How to test for a method call

In cypress there is multiple methods for spying or stubbing the navigator methods for instance :

```js

context('window.console', () => {
    before(() => {
        cy.visit('/');
    });

    it('Check console methods called', () => {
        cy.window()
            .then((w) => {
                cy.spy(w.console, "log");               
                cy.get("#btn-console-do-test").click()
                    .then(() => {
                        expect(w.console.log).be.called.calledTwice;
                    });
            });
    });
}
);
```

This test clicks on a button and then expect the console.log to be called twice.


## The problem with JSInterop

This would work very well in a pure js application. If the console.log method call are done with JSInterop like in a Blazor WASM app with this code

```cs
await jsRuntime.InvokeVoidAsync("console.log","test");
//or
Console.WriteLine("test");
```

Then it would fail with this error

```
blazor.webassembly.js:1 WASM: ﻿Unhandled exception rendering component:
blazor.webassembly.js:1 WASM: Microsoft.JSInterop.JSException: The value 'window.console.log' is not a function.
blazor.webassembly.js:1 WASM: Error: The value 'window.console.log' is not a function.
blazor.webassembly.js:1 WASM:     at p (http://localhost:5000/_framework/blazor.webassembly.js:1:9063)
blazor.webassembly.js:1 WASM:     at http://localhost:5000/_framework/blazor.webassembly.js:1:9605
blazor.webassembly.js:1 WASM:     at new Promise (<anonymous>)
blazor.webassembly.js:1 WASM:     at Object.beginInvokeJSFromDotNet (http://localhost:5000/_framework/blazor.webassembly.js:1:9579)
blazor.webassembly.js:1 WASM:     at _mono_wasm_invoke_js_marshalled (http://localhost:5000/_framework/wasm/mono.js:1:165611)
blazor.webassembly.js:1 WASM:     at wasm-function[6221]:0x11936a
blazor.webassembly.js:1 WASM:     at wasm-function[1431]:0x402ee
blazor.webassembly.js:1 WASM:     at wasm-function[636]:0x147cf
blazor.webassembly.js:1 WASM:     at wasm-function[4996]:0xeb135
blazor.webassembly.js:1 WASM:     at wasm-function[3247]:0xa0666
blazor.webassembly.js:1 WASM:   at System.Threading.Tasks.ValueTask`1[TResult].get_Result () <0x20a9640 + 0x0002c> in <5745b1bd6f4246d7aee8c81307e6355a>:0 
blazor.webassembly.js:1 WASM:   at Microsoft.JSInterop.JSRuntimeExtensions.InvokeVoidAsync (Microsoft.JSInterop.IJSRuntime jsRuntime, System.String identifier, System.Object[] args) <0x2081800 + 0x000e4> in <3eedf0ca90ca4e72bf6870618ca98c7c>:0 
```

Tgis is due to this code in JS you can find in [this file](https://github.com/dotnet/extensions/blob/master/src/JSInterop/Microsoft.JSInterop.JS/src/src/Microsoft.JSInterop.ts) :

```ts
function findJSFunction(identifier: string): Function {
    if (cachedJSFunctions.hasOwnProperty(identifier)) {
        return cachedJSFunctions[identifier];
    }

    let result: any = window;
    let resultIdentifier = 'window';
    let lastSegmentValue: any;
    identifier.split('.').forEach(segment => {
        if (segment in result) {
        lastSegmentValue = result;
        result = result[segment];
        resultIdentifier += '.' + segment;
        } else {
        throw new Error(`Could not find '${segment}' in '${resultIdentifier}'.`);
        }
    });

    if (result instanceof Function) {
        result = result.bind(lastSegmentValue);
        cachedJSFunctions[identifier] = result;
        return result;
    } else {
        throw new Error(`The value '${resultIdentifier}' is not a function.`);
    }
}
```

You see the problem ? No ? Well it's not easy : 
- types are defined by window. eg : if you have an iframe then the type "Function" inside the iframe is not the same as the "Function" type in the parent. 
- cy uses iframes for running the tests (that's why you are not limited like in WebDriver)
- when you call cy.spy, it changes the definition of console.log, so its type becomes a "Function" in the context of the test runner iframe.

## How do I fix this ?

Fortunately js allows us to do very stupid things, like change an object prototype on the fly ! After the fix, my code cypress test becomes this

```js

context('window.console', () => {
    before(() => {
        cy.visit('/console');
    });

    it('Check console methods called', () => {
        cy.window()
            .then((w) => {
                cy.spy(w.console, "log");
                w.console.log.__proto__ = w.Function;
                cy.get("#btn-console-do-test").click()
                    .then(() => {
                        expect(w.console.log).be.called.calledThrice;
                    });
            });
    });
}
);
```

Notice the line after the spy ([which is synchronous](https://docs.cypress.io/api/commands/spy.html#Syntax)) which changes console.log prototype, tgis makes the "instanceof Function" condition pass and my test run successfully.

I created the following Cypress command to reduce code duplication

```js

Cypress.Commands.add('spyFix', (object, method, window) => {
    cy.spy(object, method);
    object[method].__proto__ = window.Function;
});

context('window.console', () => {
    before(() => {
        cy.visit('/console');
    });

    it('Check console methods called', () => {
        cy.window()
            .then((w) => {
                cy.spyFix(w.console, "log");
                cy.get("#btn-console-do-test").click()
                    .then(() => {
                        expect(w.console.log).be.called.calledThrice;
                    });
            });
    });
}
);
```

## Conclusion

I'm happy I found an easy way to fix this, there is other ways with new win.Function or by overriding some window.DotNet functions but they require more line of code and are ore complicated.