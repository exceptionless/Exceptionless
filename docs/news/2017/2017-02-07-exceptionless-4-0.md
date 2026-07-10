---
title: "Exceptionless 4.0 is Here!"
---

# Exceptionless 4.0 is Here!

![Exceptionless 4.0 Announcement](/assets/img/news/exceptionless-4.png)That's right folks, Exceptionless 4.0 is here and we've got the full low-down on all the improvements, upgrades, and enhancements in this week's blog article, along with how to upgrade instructions for self hosters.

The primary focus of version 4.0 was to add support for Elasticsearch 5. By migrating from Elasticsearch 1.7 to 5, we removed growing technical debt and benefited greatly in the process. Here is an overview of the benefits gained by migrating to Elasticsearch 5 in 4.0.0.

## Performance

Since we first migrated to Elasticsearch 1.x from MongoDB, we've learned a lot of hard lessons. With 4.0 and the upgrade to Elasticsearch 5, we took all those lessons and applied them to the new release and infrastructure setup. Here are some performance improvements and notes that came out of those implementations.

* We've moved from monthly indexes to daily indexes. This change means that when you are filtering by the last 24 hours or last week, you are greatly reducing the amount of work Elasticsearch has to do to return the requested data.

* Because of this, the average use case will see between **25% and 80% improvement to indexing** throughput. This means that, with the same hardware, we can index the same documents much faster!

* We moved from Groovy scripts to Painless scripts, which are **four times faster**. By moving to Painless scripts, we also greatly simplified the burden of having to modify configuration files to enable scripting!

* Elasticsearch has added a lot of new data types since 1.7, and we are now taking advantage of them to **reduce memory, storage and query costs.** This means we can query the same data faster, using less memory doing it. We've also set up more sensible defaults to ensure we don't index very long strings.

* We've also made several performance and reliability improvements to snapshots (backups).

## Self Hosting improvements

Our goal is to have everyone be able to setup and use Exceptionless in a matter of minutes. One of the areas that had hendered a lot of self hosters in the past was forgetting to update the elasticsearch.yml file, which is no longer an issue with the migration to Painless scripts. If you're a self hoster, check out the

* The move to using Painless scripts as part of bulk updates and ingest pipelines reduced the burden of having to modify configuration files to enable scripting! Long gone are the times where you would have to reset your setup because you missed a configuration step.

* We've also added various maintenance jobs that handle backups and restores automatically, so you don't have to worry about losing your data!

* **Future upgrades should be seamless** as Elasticsearch now handles reindexing out of the box! Once you have migrated your data to Elasticsearch 5, we think future major upgrades will just be handled by the Exceptionless app itself!

* You can now also use [Docker](https://hub.docker.com/_/elasticsearch) or [Azure arm templates](https://github.com/elastic/azure-marketplace) to quickly set up a cluster. We really like this direction and will continue moving down this path, hopefully getting to the point where you can click a single button and have an Exceptionless production instance running locally!

## Dashboards

With Exceptionless 4.0.0, we continued to focus on finishing up backend improvements to both our [repositories](https://github.com/FoundatioFx/Foundatio.Repositories) and [parsers](https://github.com/FoundatioFx/Foundatio.Parsers) that we made in the 3.5 releases. We feel that **all the pieces are finally in place to allow us to do custom dashboards in the near future**, something we talk about in our [2017 Roadmap blog post](/2017-exceptionless-feature-functionality-and-enhancement-roadmap/).

[Foundatio.Parsers](https://github.com/FoundatioFx/Foundatio.Parsers) now gives us the ability to define and validate custom aggregations using Lucene-style syntax. For example, lets say I want a date histogram that shows the min, max and average event value. I'd just need to pass `date:(date min:value max:value avg:value)` to the following endpoint: [`/api/v2/events/count?aggregations=date:(date min:value max:value avg:value)`](https://api.exceptionless.io/docs/index.html#!/Event/Event_GetCountAsync) to return aggregations across a time series!

## Upgrading to 4.0.0

The only users that need to worry about upgrading anything for this new release are self hosters. If you are self hosting Exceptionless, please review the [Self Hosting Documentation](/docs/self-hosting/), which contains information about upgrading your existing install.

Also, please take a look at the [change log](https://github.com/exceptionless/Exceptionless/compare/v3.5.1...v4.0.0) for a full list of the changes.

## Always Improving

We’re always striving to improve the efficiency of Exceptionless and all of our projects. If you see any room for improvement or have any comments when using anything from us, please send us an in-app message, [submit a GitHub issue](https://github.com/exceptionless/Exceptionless/issues) or [contact us](/) on the website.

## And Lastly, Thanks!

We'd like to say thank you to the community, project sponsors, and the Elasticsearch team for helping us ship 4.0.0. You guys rock!

### Want to know what's coming next?

[Check out the 2017 Roadmap](/2017-exceptionless-feature-functionality-and-enhancement-roadmap/) and let us know your thoughts on our planned development.
