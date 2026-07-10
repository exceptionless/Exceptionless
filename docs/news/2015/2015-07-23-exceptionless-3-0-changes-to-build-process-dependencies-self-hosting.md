---
title: "Exceptionless V3.0 - Changes to the Build Process, Dependencies, and Self Hosting"
---

# Exceptionless V3.0 - Changes to the Build Process, Dependencies, and Self Hosting

![version-3-featured](/assets/img/news/version-3-featured.png)Version 2.0 was a pretty big rewrite for us, and we're happy with how everything played out, but that doesn't mean we're done!

We've been working on 3.0, and it's ready.

What was the focus, you ask? To make life **easier** for **you**! We've simplified the build process, removed dependencies, and drastically improved the ease of self hosting.

Check out the details, below, and upgrade today!

## Removed Dependencies

Previously, MongoDB was a major dependency that increase the complexity of the overall project. All the data previously hosted in MongoDB is now hosted in ElasticSearch, making it **super easy** for users to self host or develop Exceptionless since you only need to set up ElasticSearch (which you had to do anyway).

With this, Redis is no longer configured by default, but you can set it up easily by setting the connection string. We definitely recommend using it.

Removal of the MongoDB dependency brings us one step closer to running Exceptionless on vNext, on any operating system. We hope to achieve this soon, but do not have a timeline.

## Easier Self Hosting

The goal is to make self hosting **as easy as possible** so anyone can set it up and try Exceptionless out.

With Exceptionless 3.0, we now have a single build artifact that contains bo the SPA app and the API end server, with default configuration. The ZIP file contains a batch file you can run to download and start ElasticSearch, launch IIS Express with a temp website, and load your browser automatically with the Exceptionless test instance. This lets you load up everything and play around with Exceptionless **in a single click!**

Another change to the configuration is that you now have the ability to set every Exceptionless API Setting via Environmental Variables.

Check out the documentation for [detailed Self Hosting Configuration docs](/docs/self-hosting/).

## Simplified Build Process

In order to make it easier and faster to deploy, we removed the [OctopusDeploy](http://octopusdeploy.com) build dependency and are now using [Azure Git deploy](https://azure.microsoft.com/en-us/documentation/articles/web-sites-publish-source-control/), which pulls directly from a GitHub repository that contains the build artifacts.

With this move, our mindset changed regarding the master branch. For us it means production, but for you it means that whatever is in our master branch is stable and currently deployed to live. We **no longer have to wait** for a build to complete, create a production release in OctopusDeploy, then manually deploy it, we just commit to git and the rest is history!

Expect more on this topic from us soon. In the mean time, enjoy the simplified build process.

## Upgrading to 3.0

The only users that need to worry about upgrading anything for this new release are self hosters. If you are self hosting Exceptionless, please review the [Self Hosting Documentation](/docs/self-hosting/), which contains information about upgrading your existing install.

## So What?

Well, what all this means for you is that we will be able to update things much quicker, and that life just got way easier for all you self hosters out there!

Let us know what else we can do to make life easier for you.
