---
title: "Universal JavaScript Support Added to Exceptionless.JavaScript"
date: 2017-08-30
---

# Universal JavaScript Support Added to Exceptionless.JavaScript

![Exceptionless Universal JavaScript](/assets/img/news/universal-javascript-1024x538.jpg)

Recently, we released [Exceptionless.JavaScript 1.5](/news/2017/2017-08-15-javascript-client-v1-5-release-details-notes). The major update for the release was the addition of universal JavaScript (React Universal) support! More details below. The key is that we can now run in server side node apps, or in the browser, with a single script and do the right thing!

> TL;DR: Isomorphism is the functional aspect of seamlessly switching between client- and server-side rendering without losing state. Universal is a term used to emphasize the fact that a particular piece of JavaScript code is able to run in multiple environments. - [Gert Hengeveld, Isomorphism vs Universal JavaScript](https://medium.com/@ghengeveld/isomorphism-vs-universal-javascript-4b47fb481beb)



We have gotten lots of requests for Universal JavaScript support. What this means is that you can use a library in the browser or server without any changes from the end user. This is great because you can just consume the library and just know it will work using the same API service no matter where you want to use it. The main con of using a universal client is the increased payload size as you know have to send node support to the browser as well.

We implemented universal JavaScript support by bundling both the browser and node scripts together. But it wasn't as easy as concatenating the files together. We had to refactor the node and browser entry points so that they would automatically run, while ensuring that the initialization of environment specific code (storage, network, etc.) only ran when specific environment conditions were met. This is pretty easy to do with an [IIFE function](https://en.wikipedia.org/wiki/Immediately-invoked_function_expression) and a quick if check as shown [here](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v1.5.4/src/exceptionless.ts#L14-L38). Next, we needed to provide a [new entry point that imported both of the entry points](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v1.5.4/src/exceptionless.universal.ts) which ensures that browser and node entry points run.

This update adds support for universal apps, and a React Universal sample that shows the exact changes that were required to get everything working and setup can be found [here](https://github.com/niemyjski/react-redux-universal-hot-example/commit/7f7c01ca1b328f3389c3919a53376bccbbfe1f08). The pull request for universal support can be found [here](https://github.com/exceptionless/Exceptionless.JavaScript/pull/75).

**To target it**, you just need to reference the universal script (exceptionless.universal.js), this will happen automatically if you install via browserfiy or webpack.

## Questions? Feedback?

Let us know what you think about the new Universal JavaScript support over on [GitHub](https://github.com/exceptionless/Exceptionless.JavaScript/issues)!
