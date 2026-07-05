---
title: "JavaScript & Node.js Client Version 1 Release Candidate"
date: 2015-06-04
---

# JavaScript & Node.js Client Version 1 Release Candidate

![Exceptionless JavaScript Client](/assets/img/news/javascript-release-candidate.jpg)

If you've been following along the last few weeks, you know we've been working hard to get the new [JavaScript Client](/javascript-client-available-for-preview-testing/) up to speed and ready for a version 1 release.

**We think we're there!**

Whether you're a [JavaScript](/javascript-client-demo-exceptionless/) or [Node.js](/exceptionless-node-js-javascript-client-demo/) user, you'll be able to enjoy the same full featured exception and event reporting platform that our primary .NET client offers, with fewer platform specific boundaries.

## Sure It's Ready?

We have been doing extensive testing over the course of the last month, which has allowed us to identify issues and inefficiencies throughout. Each of those has been addressed with several improvements and fixes, leaving us with a much faster, more stable client.

Many of the tweaks we made were related to IE9 and Node. Those issues have been resolved and things are working well now. In addition, we further increased performance by shrinking the library size fairly drastically.

Moving forward, we will just be working on bug fixes related to user-reported issues as usage picks up.

### Recent Bug Fixes, Issue Resolutions, & Improvements

* Ensured compatibility with module formats like [es6](http://wiki.ecmascript.org/doku.php?id=harmony:specification_drafts) ([SystemJS](https://github.com/systemjs/systemjs)/[jspm](http://jspm.io/)) and [RequireJS](http://requirejs.org/)
* Various IE9 and Node compatibility issues fixed
* Decreased library size to improve performance and efficiency
* Various other performance improvements
* Fixed Angular integration failures with Angular Router
* Fixed - Unable to integrate with Aurelia due to node being improperly detected
* Changed the implementation of the InMemoryStorage to do a get instead of a get and delete
* Unable to post events with the NodeSubmissionClient over http fixed
* Fixed - Unit tests are failing due to transpilation

## Start Using It!

We've already released a few blog posts (linked below) that detail how to get up and running, but please visit the [Exceptionless.JavaScript GitHub Repo](https://github.com/exceptionless/Exceptionless.JavaScript) for the most up-to-date documentation.

**[JavaScript Users](/javascript-client-demo-exceptionless/)** can find installation, configuration, usage details and examples [here](/javascript-client-demo-exceptionless/).

If you're a **[Node.js](/exceptionless-node-js-javascript-client-demo/) user**, follow [this article](/exceptionless-node-js-javascript-client-demo/) to get set up and running.

We've also got [**examples/samples on GitHub**](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example) for JavaScript, Express, TypeScript, SystemJS, and RequireJS.



  [Try It Today!](https://github.com/exceptionless/Exceptionless.JavaScript)



## Let Us Know What You Think

We're suckers for feedback, so let us have it whether good, bad, or indifferent. Bugs, etc should be reported on the [GitHub Issues](https://github.com/exceptionless/Exceptionless.JavaScript/issues) page, but feel free to shoot us an in-app message, email, etc and let us know what you think and if you had any issues getting everything working.
