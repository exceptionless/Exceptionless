---
title: "Server Side Technology the Exceptionless Team Uses"
---

# Server Side Technology the Exceptionless Team Uses

![Server Technology](/assets/img/news/servers.jpg)As it turns out, building technology and web applications takes a **lot** of other technology. We were reflecting the other day and decided it would be cool if we did a blog series that went over all the different "stuff" we use to make and maintain Exceptionless.

For the first part in the series, we decided to look at all the **server-side products and services**. They are listed below, along with a short description of what they are used for. See something you've never used? Check it out! Have something we should try out instead? Let us know by commenting!

## The Server Tech

### TeamCity

**Continuous integration and deployment triggering**

[TeamCity](http://www.jetbrains.com/teamcity/) from JetBrains is a popular continuous integration server that supports a variety of different version control systems and build runners.

### Octopus Deploy

**Automated deployment**

[Octopus Deploy](http://octopusdeploy.com/) works with, not against your build server. This ensures reliable, secure, automated releases of ASP.NET applications and Windows Services into test, staging and production environments, whether they are in the cloud or on-premises. The Octopus Deploy dashboard can tell you quickly which versions of your application are deployed to specific environments. .

### ElasticSearch

**Event storage, filtering, stats and search**

[Elasticsearch](http://www.elasticsearch.com/) is a search server. is a distributed, multitenant-capable full-text search engine with a RESTful web interface and schema-free JSON documents. It has scalable search, real-time search, multi tenancy and can be used to search all kinds of documents.

### MongoDB

**Account and billing info storage**

[MongoDB](http://www.mongodb.org/) makes integration of data in certain types of applications easier and faster. It is a cross-platform document-oriented database that is classified as a NoSQL database. It favors JSON-like documents with dynamic schemas over the traditional table-based relational database structure.

### Redis

**Caching**

[Redis](http://redis.io/) is often referred to as a data structure server. Keys can contain strings, hashes, lists, sets, sorted sets, bitmaps and hyperloglogs. It is open source and licensed under BSD.

### Resharper

**Makes Visual Studio awesome!**

[ReSharper](http://www.jetbrains.com/resharper/) is a productivity tool that makes Microsoft Visual Studio a much better IDE. It includes features such as code inspections, automated code refactorings, blazing fast navigation, and coding assistance.

### Postman

**For testing our REST API**

[Postman](http://www.getpostman.com/) allows you to construct simple as well as complex requests quickly. You can save them for later use and analyze the response sent by the API. It dramatically cuts down the time required to test and develop APIs. It can easily scale and be used for your small team, or larger organizations.

### Simple Injector

**Dependency Injection**

[Simple Injector](https://simpleinjector.codeplex.com/) is an easy-to-use Dependency Injection (DI) library for .NET 4+. It supports Silverlight 4+, Windows Phone 8, Windows 8 including Universal apps and Mono. It can be easily integrated with popular frameworks such as Web API, MVC, WCF and many others. It also provides a carefully selected set of features in its core library to support many advanced scenarios.

### SignalR

**Real time notifications**

[SignalR](http://signalr.net/) is a library for ASP.NET developers that assists with developing real-time web functionality. SignalR allows bi-directional communication between server and client. It allows pushing content to connected clients instantly as it becomes available. SignalR supports Web Sockets, and falls back to other compatible techniques for older browsers easily as well as includes APIs for connection management, grouping connections, and authorization.

### AutoMapper

**Easy mapping of data models from one form to another**

[AutoMapper](https://github.com/AutoMapper/AutoMapper) is a convention-based object-object mapper. It provides useful conventions to take the tedious work out of figuring out how to map type A to type B. As long as you follow a few simple standards, almost zero configuration is needed to map two types.

### FluentValidation

**Validating business rules in our data model**

A small validation library for .NET, [FluentValidation](http://fluentvalidation.codeplex.com/) uses an easy-to-use interface and lambda expressions for building validation rules for your business objects quickly and efficiently.

### ApprovalTests

**Easy verification of test outputs**

[ApprovalTests](http://approvaltests.sourceforge.net/) is an open source assertion/verification library to assist with unit testing. It is compatible with many .NET unit testing frameworks such as Nunit, MsTest, Xunit, and MBUni. It can be used for verifying objects that require more than the simplest of asserts.

## What Did We Miss?

We know you've got some cool toys you use on your projects. Spit it out! What other technology should we give a try. We're always looking for the best, fastest, coolest stuff to try out.
