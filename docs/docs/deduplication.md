---
title: "Event De-Duplication"
---

# Event De-Duplication

Have you ever run into an error where the event that triggers the error happens rapidly in a short amount of time? Infinite loops combined with errors? Ugh. If that happens, we don't think you should have to worry about your event quotas and plan limits.

That's why we built automatic de-duplication.

If we get two events that are exactly the same within a minute, we send the first, cancel the second and hold onto it for 60 seconds. If that same exact event comes in again, we cancel it and increment the count on the one we were holding. Once 60 seconds is released we submit that event and now you get a sampling of events during that period with the exact account that occurred. We only do this if the event is an exactly the same minus date occurred.

---

[Next > Integrations](/docs/integrations)
