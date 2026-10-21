---
title: "Log Levels"
metaTitle: "Setting log levels in Exceptionless"
description: "Set minimum log levels for Exceptionless log events in project settings or per log stack, so clients stop sending lower-level logs without a redeploy."
---

# Log Levels

Setting log levels allows you to control the flow of data into your Exceptionless account. This is important when you are trying to filter the signal from the noise. In most error monitoring services, you would need to manually configure what log levels are used to send events by customizing your code. With Exceptionless, you can update the log levels used right in the Exceptionless UI.

To configure your default log level, open **Settings > Projects** in the sidebar, click your project, and then open the **Settings** tab. The **Default Log Level** section is at the top. Click the dropdown and pick the lowest level you want to keep.

![Default Log Level dropdown on the project Settings tab](/assets/img/docs/setting-log-levels-project-default-light.webp)

You can set the level for the whole project here, or override it for a single log source on that log's stack. To find a log stack, open **Events > Logs** in the sidebar. You can also filter **Stacks > All** or **Events > All** by type.

![The Logs events view](/assets/img/docs/setting-log-levels-logs-view-light.webp)

Click a log event. The event details open with the log's stack card at the top.

### Overriding Global Log Levels

In the stack card header, click the log level button (the filter icon, first in the row of buttons) and select a new level. Events at that level or above are kept for this log source. Anything below it is no longer captured. The button only appears on log stacks that have a source.

![Log Level menu on a log stack](/assets/img/docs/setting-log-levels-stack-override-light.webp)

Once a stack has its own level, the menu also shows **Default** with the project's level. Click it to remove the override and go back to the project default.

As with Data Exclusions, updates to project level and global log level settings will be passed down to the Exceptionless client in near real-time. While the Exceptionless server will catch event that should not be included in your dashboard, the client will catch them based on your configuration and prevent them from ever being sent to the server. Read more about [Project Settings](/docs/project-settings) here.

---

[Next > De-Duplication](/docs/deduplication)
