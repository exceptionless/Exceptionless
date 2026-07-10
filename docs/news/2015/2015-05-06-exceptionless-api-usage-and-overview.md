---
title: "Exceptionless API Usage and Overview"
---

# THESE DOCS HAVE BEEN UPDATED. SEE THE [NEW DOCS HERE](/docs/api/).

![Exceptionless API](/assets/img/news/Screenshot-2015-05-06-16.26.42-300x220.png)

So you've been using Exceptionless for a while, but you wish you had a different dashboard, or maybe you'd like to integrate event data into one of your apps. No problem, **just use the API!**

Through our adventures while building Exceptionless, we've kept open source, automation, and ease of use in mind. With that, we think our API, which utilizes [Swagger](http://swagger.io/) and [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle) to automatically generate, update, and display documentation (which means it works automatically on self-hosted environments), is a great resource for our users that want to get their hands dirty and use Exceptionless data to roll their own tools, dashboards, etc.

Lets take a closer look at the API, how to use it, and some quick examples of what can be done.

## Start Using the Exceptionless API

### Accessing the API

To access the Exceptionless API, visit [https://api.exceptionless.io](https://api.exceptionless.io) and click on the "API Documentation" link to be taken to the API documentation.

### Get Your User Scoped Token

Tokens are used to access the api and have roles (scopes). When you authenticate via the login controller action, you are getting a token that impersonates your user so it has the same roles as your user account (e.g., client and user).

#### 
[Go to Auth controller action](https://api.exceptionless.io/docs/index.html#!/Auth/Auth_Login)
 and enter your login credentials.

You can enter JSON into the model field, or you an click the yellow box on the right to pre-populate the field with acceptable JSON fields. Just replace the values that you want to specify and remove the fields you don't need, like invite token.

![Exceptionless API Get User Scoped Token](/assets/img/news/01get-user-scope-token1-300x218.png)

Click "Try it out!" and generate your token. Take note of the response messages section above the button, as it details the possible codes that would be returned in the event of an error (e.g. 401 if the user name or email address was incorrect).

![Retrieve Exceptionless User Scoped Token](/assets/img/news/01get-user-scope-token2-300x105.png)

You can see from the response that it returned our token from the request url above. Take your generated token and put it in the "api_key" field at the top of the page and click "Explore." This authorizes you via bearer authentication, authenticates you to the rest api, and allows you to call controller actions.

![Add Exceptionless User Scoped Token ](/assets/img/news/02add-token-refresh-page-300x50.png)

### Get a New Token

Now, we’ll get a new token for the project we want to work on and assign it a user role (scope) of "user." We want to get a new user scoped token because we want to do more than just post events (client scoped tokens only allow you to post events), we want to retrieve them. Creating a new token also allows us to revoke the token later.

First, get your project ID from the Exceptionless Dashboard. It can be found in the URL of that project’s dashboard.

![Get Exceptionless Project ID](/assets/img/news/03get-project-ID-300x141.png)

Now, we’ll navigate to [Tokens > POST /api/v2/projects/{projectId}/tokens](https://api.exceptionless.io/docs/index.html#!/Token/Token_PostByProject), enter our Project ID, and set up our token to include the user scope and a quick note.

![Create Exceptionless Token](/assets/img/news/04get-new-token1-300x220.png)

Next, we'll click "Try it out!" and generate our new token id.

![Get new Exceptionless Token ID](/assets/img/news/04get-new-token2-300x146.png)

Now, once again, copy this new token and place it in the "api_key" field at the top of the page and click "Explore." Now everything we do will be authenticated to this new user token you’ve just created.

### Posting an Event

Now, lets post an event with a reference ID that we’ll use for a few other examples.

First, navigate to [Event > POST /api/v{version}/events](https://api.exceptionless.io/docs/index.html#!/Event/Event_PostAsync)

![Exceptionless API Post Event](/assets/img/news/05post-event1-300x204.png)

You’ll see a few basic examples of events and some explanation of the resource in this panel. Make sure to give it a read. For this example, we’ll use a simple log event, with a brief message, and add a reference ID to it. _Note that you must also enter the current API version in the "version" field._

When we click "Try it out!" and get a 202 response code, we know we’ve created an event.

![Exceptionless API Created Event](/assets/img/news/05post-event2-e1430946143322-300x92.png)

### Get Event by Reference ID

If we want to get the event we just created by it’s reference_id, we can navigate to [Event > GET /api/v2/events/by-ref/{referenceId}](https://api.exceptionless.io/docs/index.html#!/Event/Event_GetByReferenceId), enter that reference ID, and get back the details of the event.

![Get Exceptionless Event by ReferenceID](/assets/img/news/06-get-by-reference-ID2-300x271.png)

### Getting the Event via a Search Filter

Another example of getting an event may include using the reference ID or another search filter we just created and getting all by a reference filter. You can use any [search filter](/docs/filtering-and-searching) in the filter parameter.

To do so, navigate to [Event > GET /api/v2/events](https://api.exceptionless.io/docs/index.html#!/Event/Event_Get) and use the reference term to filter events by the reference ID.

[![Exceptionless API Get All Filter Search](/assets/img/news/07-get-all-filter-reference-300x168.png)](/assets/img/news/07-get-all-filter-reference.png)

Results

![Exceptionless API Get Al Filter Search Results](/assets/img/news/07-get-all-filter-reference2-300x183.png)

### Get Organizations and Projects

Naturally, we can get all the organizations or projects associated with the current authorized token, as well.

#### Organizations

Navigate to [Organization > GET /api/v2/organizations](https://api.exceptionless.io/docs/index.html#!/Organization/Organization_Get) and click "Try it out!"

![Exceptionless API Get Organization Results](/assets/img/news/08get-organizations2-300x202.png)


#### Projects


Navigate to [Project > GET /api/v2/projects](https://api.exceptionless.io/docs/index.html#!/Project/Project_Get) and click "Try it out!"

![Exceptionless API Get Projects Results](/assets/img/news/09get-projects2-300x204.png)


## How to Authenticate to the API


### 1. Bearer Authentication

The api documentation uses bearer authentication to authenticate to the API. You can do this in your apps too by specifying a bearer authorization header with your token as shown below.

![Exceptionless Bearer Authentication](/assets/img/news/10-bearer-auth-key-300x164.png)

### 2. Authenticate via the Query String

Everything we’ve shown you today can be easily and cleanly accessed via a URL query string and your access token.

For example, if we want to view our organizations, we simply navigate to https://api.exceptionless.io/api/v2/organizations, add the query string "?access_token={token}" and press enter to get the data.

![Exceptionless Query String API Authentication](/assets/img/news/11-url-query-string-version-300x145.png)

## Let Us Know What You Think!

We've tried to make the API as easy and intuitive to use as possible, but we're always open to feedback and comments, so please let us know what could be better, easier, faster, etc.

And, of course, if you have any questions about the API, please leave a comment here, send us an in-app support message, or simply submit the website [contact form](/contact/ "Contact Exceptionless").
