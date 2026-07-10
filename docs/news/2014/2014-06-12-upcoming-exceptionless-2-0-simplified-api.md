---
title: "More from the Upcoming Exceptionless 2.0: Simplified API"
date: 2014-06-12
---

# More from the Upcoming Exceptionless 2.0: Simplified API

![Exceptionless 2.0 API Simplified](/assets/img/news/v2-api.png)Since [going open source](/fork-us-exceptionless-goes-open-source/ "Fork Us! Exceptionless Goes Open Source"), we've wanted to simplify the API and make it easier to work with.

We're taking the time to do it now, and it's going to be **awesome!**

Exceptionless 2.0, [coming soon](/exceptionless-2-in-the-making/ "Exceptionless 2.0 – In the Making"), will have a new, manageable API with tons of great documentation and examples. Take a look at the preliminary documentation at the below link, and make sure to give us any feedback you might have.

### API Simplified

<ul>
<li><a href="https://api.exceptionless.io/docs/" target="_blank">New REST API documentation and samples site.<br></a>Take a look and let us know what you think<span>Exceptionless API Documentation</span><a href="https://api.exceptionless.io/docs/" style="color: #4183c4;"><br></a></li>
<li>Event POSTs take the raw data and use a plugin system to interpret that data and translate them into events.
<ul>
<li>This allows us to take literally any data and turn it into events in the system.</li>
<li>The POST data is captured as a raw bytes and added immediately added to a queue for processing.</li>
<li>Plugins can easily be created to support new data formats like system logs.</li>
</ul>
</li>
<li>This simplified API will make creating libraries for other platforms dead simple.</li>
<li>The API lives in a separate project and can be hosted on high-performance systems like the new Helios IIS host.</li>
<li>Makes it easy for us to migrate the UI to a SPA app.</li>
<li>Now uses OAuth 2.0 in addition to supporting API tokens.</li>
<li>Highly consistent REST API modeled after GitHub and Stripe.</li>
<li>It's so simple you can just use CURL as a client.</li>
</ul>

We hope you're as excited as we are to have this new, improved, more complete, and more usable documentation. Stay tuned for more details on the upcoming Exceptionless 2.0, and don't forget to leave a comment letting us know what you think.
