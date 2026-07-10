---
title: "How to Self-Host Your Error Monitoring Service"
date: 2020-09-30
---

# How to Self-Host Your Error Monitoring Service

The beauty of open-source solutions is that you are often given the option to self-host or pay for an easy hosted solution by the company or people who created the project. This is true of many projects. [Ghost](https://ghost.org) is a popular example of this from the blogging world. Some analytics serves, including [Matomo](https://matomo.org/matomo-on-premise/), allow users to choose between a hosted and a self-hosted solution. In that same vein, [Exceptionless](https://exceptionless.com) provides a self-hosted option for those who would like to host their own error monitoring.

Today, we're going to walk through setting up a self-hosted Exceptionless instance. Let's get started!

Exceptionless provides a simple Docker image to help get started with self-hosting. We're going to make use of that, so the first step is to [download Docker for desktop](https://www.docker.com/get-started) if you don't already have it. Once you're able to download that and start it, you should then be able to execute `docker` commands from the command line. Test it out by running `docker stats` in the command line.

Pretty cool, right? Just by downloading and running the desktop application, you also have access to the Docker CLI. That CLI is what we'll need to run our self-hosted instance of Docker.

Let's make sure we can get Exceptionless running locally. Ready for how easy this is? Are you?

Ok, start up Docker Desktop, then in your command line, run:

`docker run --rm -it -p 5200:8080 exceptionless/exceptionless:latest`

This will check to see if you've already downloaded the latest Exceptionless release, and if not, it will install all of the necessary dependencies. This is important because Exceptionless is split into a client-side front-end and a server-side back-end. Docker lets all of this be combined.

When the process in your command line finishes up, open your browser and navigate to `http://localhost:5200`. If all went well, you should see the Exceptionless login page.

[![Exceptionless self-hosted login page](/assets/img/news/self-hosted-login.png)](/assets/img/news/self-hosted-login.png)

Go ahead and sign up for an account. Keep in mind, this is not a good production solution, but it's a great way to get started with a self-hosted error monitoring solution.

Along with the front-end that we're looking at now, you also have a full Exceptionless server running. To prove it, let's run a simple cURL command.

```shell
curl --location --request POST 'http://localhost:5200/api/v2/auth/login' \
  --header 'Content-Type: application/json' \
  --data-raw '{
      "email": EMAIL_ADDRESS,
      "password": PASSWORD
  }'
```

Make sure to use the email and password you just signed up with. You should get a token back. This shows you that the API is running successfully and you can now do everything you would with a hosted Exceptionless instance, but locally. Go ahead and try it out. [Here are the full API docs for Exceptionless](https://api.exceptionless.io/docs/index.html).

The problem here is the data you save in this run of your self-hosted Exceptionless instance will not be saved between runs. As soon as you shut down Docker and exit the application, your data will be deleted and you'll be starting from scratch. That's no fun. Let's fix it.

Go ahead and shut down Exceptionless either by exiting in the command line or by going into your Docker Desktop Dashboard and clicking the stop button your Exceptionless instance. Once it's stopped, open up your command line again and run:

```shell
docker run --rm -it -p 5200:8080 \
    -v $(pwd)/esdata:/usr/share/elasticsearch/data \
    exceptionless/exceptionless:latest
```

If you're using PowerShell, you'll instead want to run:

```shell
docker run --rm -it -p 5200:8080 `
    -v ${PWD}/esdata:/usr/share/elasticsearch/data `
    exceptionless/exceptionless:latest
```

Now, when you sign up at `http://localhost:5200`, your data will be persisted. You can start tracking errors locally and that data will be shown in your Exceptionless dashboard even after you shut down Docker/Exceptionless and restart it.

Your homework: Try running this locally with SSL and SMTP enabled. It's just as simple as everything else we've done so far because Exceptionless has really taken the time to make sure the developer experience is top-notch. [Check out the instructions here](/docs/self-hosting/docker#simple-setup-wssl-support-and-smtp).

Error monitoring is incredibly important to both your customer's experience and your own sanity. To give yourself true control over error monitoring, you can choose a solution like Exceptionless, and as you've seen here, self-host it pretty easily. Of course, if you'd rather let Exceptionless take care of hosting, [that's covered too](https://exceptionless.com).

Now, you can go and experiment with your newly installed, self-hosted, error monitoring service. Will you let it run on your machine? Will you tinker and get it production-ready? Will you deploy it to a remote server somewhere? (That last one might be the subject of a future post 😉)
