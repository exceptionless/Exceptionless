---
title: "The Power of Real-time Project Settings"
date: 2015-06-17
---

# The Power of Real-time Project Settings

Did you know there are customizable server/client configuration values within your Exceptionless Project's settings? Well **now you do** - and knowing is half the battle!

These project level settings are a dictionary of key value pairs (string key, string value). They are defined server-side and automatically pushed to the client when they are updated. Using this feature allows you to control what you send without redeploying your app, which we think is pretty cool!

**You can use these client configuration settings for a variety of applications, including:**

* Controlling [data exclusions](/docs/security) for protecting sensitive information
* Enabling / Disabling user signups
* Turning logging on or off
* Enabling analytics
* Controlling information collection
* And many more! You can send any key value pair to control whatever you like within your app.

Let's take a look at a JavaScript and .NET client usage example to get your rolling with this feature.

## Adding a New Client Configuration Value

![Exceptionless Client Configuration Settings](/assets/img/news/project-settings-page-300x209.png)

Before we get started with more of an explanation and an example, we need to add a new key and value. To do so, we go to `Admin > Projects` in our Exceptionless Dashboard, select the project we are working on, then go to the "Settings" tab.

This is where we can add a "New Client Configuration," which simply consists of the key and value. For the example below, we'll add the (fictional) `enableLogSubmission` key and set it to `true`.

## How it Works

When your application first starts up, your project's client configuration settings are read (from storage) and applied.

If a setting value doesn’t exist in storage, it will be retrieved from the server after the next event submission occurs. We do this by inspecting the response headers and comparing a response header that contains the setting version. If the version doesn’t match the saved setting value then we make a request to get the setting and apply it.

_It’s worth noting that we allow you to define your own default settings and overwrite them with server side settings, which we'll include in our example._

### How do I use the client configuration settings?

In the example below, we will dynamically turn on or off the log event submissions at runtime without restarting the app or logging into the server to change configuration settings.

**Why, you ask?** Maybe we don't care about log submission until there is a really tough issue to solve. With Client Configuration Values, we can simply turn it on only when needed.

We’ll assume for this example that we are using the `enableLogSubmission` key we created above to control this. This setting is made up and doesn’t have to exist server side since we will be providing a default value client side. This allows us to define it via the project settings page at anytime and change our applications behavior.

To control this we will be registering a new [client side plugin](/how-to-add-a-plugin-to-affect-events-in-exceptionless/) that runs every time an event is created. If our key (`enableLogSubmission`) is set to false and the event type is set to log, we will discard the event.

### .NET Example

```cs
ExceptionlessClient.Default.Configuration.AddPlugin("Conditionally cancel log submission", 100, context => {
    var enableLogSubmission = context.Client.Configuration.Settings.GetBoolean("enableLogSubmission", true);

    // only cancel event submission if it’s a log event and enableLogSubmission is false
    if (context.Event.Type == Event.KnownTypes.Log && !enableLogSubmission) {
        context.Cancel = true;
    }
});
```

You might notice that we are calling the `GetBoolean` method to check the `enableLogSubmission` key. This is a helper method that makes it easy to consume saved client configuration values. The first parameter defines the settings key (name). The second parameter is optional and **allows you to set a default value** if the key doesn’t exist in the settings or was unable to be converted to the proper type (e.g., a boolean).

#### .NET Helpers

Above, we used the `GetBoolean` helper method. In the .NET client, we have a few helpers to convert string configuration values to different system types. These methods also contain overloads that allow you to specify default values.

**Helper List**

* `GetString`
* `GetBoolean`
* `GetInt32`
* `GetInt64`
* `GetDouble`
* `GetDateTime`
* `GetDateTimeOffset`
* `GetGuid`
* `GetStringCollection` (breaks a comma delimited list into an IEnumerable of strings)

### JavaScript Example

The same functionality above can also be achieved using our new [JavaScript Client](/news/2015/2015-06-09-javascript-node-js-client-v1-release-notes).

```js
exceptionless.ExceptionlessClient.default.config.addPlugin('Conditionally cancel log submission', 100, function (context, next) {
    var enableLogSubmission = context.client.config.settings['enableLogSubmission'];

    // only cancel event submission if it’s a log event and
    // enableLogSubmission is set to a value and the value is not true.
    if (context.event.type === 'log' && (!!enableLogSubmission && enableLogSubmission !== 'true')) {
       context.cancelled = true;
    }

    next();
});
```

## Subscribing to Setting Changes

If you would like to be notified when client configuration settings are changed, you can subscribe to them using something like the below code. This is useful when you want to update your application in real time when settings change server side.

### .NET

```cs
ExceptionlessClient.Default.Configuration.Settings.Changed += SettingsOnChanged;

private void SettingsOnChanged(object sender, ChangedEventArgs<KeyValuePair<string, string>> args) {
   Console.WriteLine("The key {0} was {1}", args.Item.Key, args.Action);
}
```

### JavaScript

```js
exceptionless.SettingsManager.onChanged(function(configuration)  {
   // configuration.settings contains the new settings
});
```

## Any Questions?

These Client Configuration Values are somewhat of a hidden Exceptionless gem, but we think they are power and that many of our users can find real value in using them to control the flow of information, specifically sensitive data.

If you have any questions or comments, please let us know. As usual, we're all ears!
