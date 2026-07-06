---
title: "March Update Rollup - New Releases, Updates, Bug fixes, and more!"
---

# March Update Rollup - New Releases, Updates, Bug fixes, and more!

March was a productive month! We got tons done, and we're here to share everything with you so you're up to date and can hold us accountable if you notice any bugs in the changes.

Let's get to it!

## Week of 3/6/2017

### Search query fix, Implemented AsyncAutoResetEvent in Foundatio, Upgraded project format

* Fixed an issue in Exceptionless where any search query starting with data. was being modified.
* In Foundatio, the main repository that will contain the default deployment and EditorConfig settings for all of our projects, an effort was made to use less locking in queues and the CacheLockProvider by using AsyncAutoResetEvent, a lighter weight async primitive.
* We also upgraded to the new VS2017 project format in our Foundatio repositories.

## Week of 3/13/2017

### Attended Elastic{ON} 17, Released Foundatio 5.0, PowerShell script update, RabbitMQ update, IMessageBus breakage, Marker interface addition

* Blake attended [Elastic{ON}](https://www.elastic.co/elasticon/conf/2017/sf) and talked to product teams about issues we've been running into, as well as the future of Elasticsearch.
* **Foundatio**
  * [Released Version 5.0 of Foundatio](https://github.com/FoundatioFx/Foundatio/releases/tag/v5.0.0)
  * We also modified an existing PowerShell script that updates existing projects to the VS2017 project format.
  * And, of course, we added some more documentation!
* **Foundatio Repositories**
  * Updated the RabbitMQ queues to use the delayed message exchange
  * Broke the IMessageBus.Subscribe API surface by changing the signature to SubscribeAsync
  * Added a marker interface when patching documents

[Watch the 3/13/2017 update video](https://youtu.be/G-faYRV7-qI?list=PLGHP7IVwFs_81fZTMgF7Dm5e0Ax4YvW_V)

## Week of 3/20/2017

### Released Exceptionless 4.0.1, Performance improvements, Bug fixes, Exceptionless.NET udpates, Foundatio Amazon SQS queues and CloudWatch metrics pull requests, Foundatio reindexing improvements

* **Exceptionless**
  * [Released Exceptionless version 4.0.1](https://github.com/exceptionless/Exceptionless/releases/tag/v4.0.1)
  * We upgraded to the latest Foundatio and Repositories builds, which brings in some performance improvements and bug fixes.
  * Visual Studio 2017 is now a requirement to debug any Exceptionless project.
  * Various bug fixes
* **Exceptionless.Net**
  * Upgraded to VS2017 project format and fixed an asp.net core bug.
  * All nightly builds for all of our NuGet packages can now be found on [myget](https://www.myget.org/gallery/exceptionless).
* **Foundatio**
  * New pull request for Amazon SQS queues and CloudWatch metrics
  * We also had some more documentation contributions!
* **Foundatio Repositories**
  * We made some improvements around reindexing.
  * Then we also fixed a few various bugs

[Watch the 3/20/2017 update video](https://youtu.be/B9gVzFmBzyY?list=PLGHP7IVwFs_81fZTMgF7Dm5e0Ax4YvW_V)

## Week of 3/27/2017

### Event metadata archiving, Amazon SQS queus and CloudWatch metrics pull requests, Blake speaks at code camp

* This week we added a setting in Exceptionless for [disabling the archiving of event metadata](https://github.com/exceptionless/Exceptionless/pull/287)
* For Foundatio, a new pull request was made for Amazon SQS queues and CloudWatch metrics. We've done a lot of work to queues and the message buses to make the constructors have options classes that get passed in. We've also done a lot of work to the Azure implementations to try and optimize them and fix bugs.
* Lastly, Blake spoke at [Northeast Wisconsin Code Camp](http://newcodecamp.com/) over the weekend on exception driven development. [The slides can be found here.](https://github.com/exceptionless/MediaKit/tree/master/presentations/exceptionless)

[Watch the 3/27/2017 update video](https://youtu.be/x9JB3BgYELQ?list=PLGHP7IVwFs_81fZTMgF7Dm5e0Ax4YvW_V)


## [WATCH NOW](https://youtu.be/njr7ang0BQg?list=PLGHP7IVwFs_81fZTMgF7Dm5e0Ax4YvW_V)




  [Watch more Live Code Demos](/category/live-coding/)

