---
title: "How to Debug Electron Apps"
date: 2021-02-22
---

# How to Debug Electron Apps

Electron is a great framework that makes developing cross-platform desktop applications easy. If you're a JavaScript developer, it is probably the first thing you'll reach for when you decide to build a desktop application. I know I did. In building my first and second desktop applications, I used Electron. In that process, I learned some tricks to help the development process go a little smoother. One such trick is how to better debug issues that may arise in the packaged, production version of your Electron app that you won't see in development. 

If you're not familiar with [Electron](https://www.electronjs.org/), it is a framework that allows you to write apps with web technologies and use them on the desktop. This is accomplished by packaging your app within its own dedicated Chromium-based application. Think of a web browser. All it is is a desktop application that allows you to explore web apps and web pages. That's what Electron does for your app. It creates a one off desktop browser. In doing so, you get access to native desktop functionalities that are not available to traditional web applications. 
    
Like with many software projects, you might find that your local development experience doesn't exactly match what happens in production. When an app is minified, built, compiled, and packaged for production use, there can be subtle changes that can break the experience of the application or break the app entirely. This is especially true when dealing with desktop applications that have more access than you might be used to with web apps. Debugging problems when your application works locally but doesn't work in its production state can be frustrating. This becomes even more frustrating in Electron when you only have access to the web application's JavaScript output in production and not the underling Electron code's output. Fortunately, we can solve this by using an error monitoring service. 

We're going to be making use of [Exceptionless](https://exceptionless.com) and the Exceptionless JavaScript client to debug and monitor our Electron application. Exceptionless is free to get started and totally open-source. Let's get started. 

From within your Electron app's project directory run `npm i exceptionless`. 

Now, we can configure the Exceptionless client and use it anywhere. This means we can use it in both the "front end" (web app) code and the "back end" Electron code. For the sake of this tutorial, we are only going to be focusing on the Electron code. Inside your `main.js` file, add the following below your other import/require statements: 

```
const { ExceptionlessClient } = require("exceptionless")
const client = ExceptionlessClient.default.config.apiKey = "YOUR API KEY"
```

You can get your project API key in the Exceptionless project settings page. 

Now, with the client configured, you can start using Exceptionless to log events. The cool thing is these don't need to just be errors. If you want to log when a particular function is called within your main Electron code, you can use `client.submitLog("Function called")` but with something more descriptive. By submitting log events for particular functions, you will know for sure the function is being called. Of course, you can and should also track errors. This is as simple as calling `client.submitException(error)` with your error. 

This is all very abstract, though. So, let's look at a practical example. Let's say your Electron app is listening to some event in order to write some data to the computer's hard disk. We need a trigger to come from our "frontend" html/js code, and then we need to read that trigger and take some action. In Electron, we use `ipcMain` to listen for events from the frontend code. An example of this might look like: 

```javascript
ipcMain.on("Save File", async (event, message) => {
  try {
    await fs.writeFileSync("/path/to/where/you/want/to/store/the/file", message)
    client.submitLog(`Wrote file successfully with the following content: ${message}`)
  } catch(e) {
    client.submitException(e)
  }
});
```

I added a log event that is sent to Exceptionless in the try and I catch the error and send that to Exceptionless in the catch. The beauty of this is we know when the event is successful, which is comforting, but we also know when it fails and why. This is important, because a failure here would be a silent failure in your app. 

Let's say the file path you think you're trying to write to does not exist after your Electron app is built and packaged (a common issue is that PATH variables exposed by default to applications can be different than what you use and have available in your development environment). If that path did not exist, the `writeFileSync` command would not work. You would have no idea why, and your users would only know it when they tried to fetch the file that should have been written. 

Imagine trying to debug that without error and event monitoring. You'd fire it up locally on your machine, run some tests, try to replicate the steps exactly as the user did them. And everything would work. You wouldn't see the error because your development environment is just different enough from the production environment to keep you from realizing that the write path in production doesn't exist. 

There are a million other ways your Electron app can fail silently. By adding error and event monitoring, you can quickly debug problems that would otherwise have you banging your head off your desk. 