---
title: "Other languages and custom clients"
navTitle: "HTTP and scripts"
description: "Send events to Exceptionless from any language, or from Bash and PowerShell scripts, with one HTTP request and a client API key."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/configure/+page.svelte
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/EventHandler.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/07_SubmissionClientPlugin.cs
---

# Other languages and custom clients

If there's no Exceptionless client for your language, you can still send errors, logs, and feature usage. Every event is
one HTTP request to the REST API. This page shows how to send events from shell scripts and from any language; the
[client specification](/docs/reference/client-spec/) covers what a full client library does on top of that.

## Before you start {#before-you-start}

You need:

- A **client API key** for your project. Copy one from the project's **API Keys** settings; see
  [Client-scoped tokens](/docs/api/project-tokens/).
- The **events URL**: `https://api.exceptionless.io/api/v2/events`.

> [!NOTE]
> On a self-hosted instance, use your own host instead, for example `https://exceptionless.example.com/api/v2/events`.

## Send events from a shell script {#shell-scripts}

The app's setup page has ready-made commands with your key filled in: open the project's **Client setup** and choose
**Command Line: Bash Shell** or **Command Line: PowerShell**. They send a log event:

```bash tab="Bash"
curl "https://api.exceptionless.io/api/v2/events" \
    --request POST \
    --header "Authorization: Bearer YOUR_API_KEY" \
    --header "Content-Type: application/json" \
    --data-binary '[{"type":"log","message":"Hello World!"}]'
```

```powershell tab="PowerShell"
$body = @{
 "type"="log"
 "message"="Hello World!"
} | ConvertTo-Json

$header = @{
 "Authorization"="Bearer YOUR_API_KEY"
 "Content-Type"="application/json"
}

Invoke-RestMethod -Uri "https://api.exceptionless.io/api/v2/events" -Method "Post" -Body $body -Headers $header
```

The server responds `202 Accepted`, and the event shows up on the project's **Events** page within a few seconds.

### Report a failed job {#report-a-failed-job}

To report a cron job or deployment step that fails, send an error when the command exits with a non-zero code. This
script uses the [GET submit endpoint](/docs/api/posting-events/#get) so `curl` can encode the message for you:

```bash tab="Bash"
#!/usr/bin/env bash
set -uo pipefail

EXCEPTIONLESS_API_KEY="YOUR_API_KEY"

report_failure() {
  curl --silent --get "https://api.exceptionless.io/api/v2/events/submit" \
    --header "Authorization: Bearer ${EXCEPTIONLESS_API_KEY}" \
    --data-urlencode "type=error" \
    --data-urlencode "source=$1" \
    --data-urlencode "message=$2" \
    --data-urlencode "tags=cron" \
    --data-urlencode "host=$(hostname)" > /dev/null
}

if ! output=$(./nightly-backup.sh 2>&1); then
  report_failure "nightly-backup" "Nightly backup failed: ${output: -1500}"
  exit 1
fi
```

```powershell tab="PowerShell"
$apiKey = "YOUR_API_KEY"

function Report-Failure([string]$Source, [string]$Message) {
  $body = @{
    "type"    = "error"
    "source"  = $Source
    "message" = $Message
    "tags"    = @("scheduled-task")
    "data"    = @{ "host" = $env:COMPUTERNAME }
  } | ConvertTo-Json

  Invoke-RestMethod -Uri "https://api.exceptionless.io/api/v2/events" -Method "Post" -Body $body `
    -Headers @{ "Authorization" = "Bearer $apiKey"; "Content-Type" = "application/json" }
}

try {
  & .\nightly-backup.ps1
  if ($LASTEXITCODE -ne 0) { throw "nightly-backup.ps1 exited with code $LASTEXITCODE" }
}
catch {
  Report-Failure "nightly-backup" "Nightly backup failed: $($_.Exception.Message)"
  exit 1
}
```

Extra query parameters, like `host` in the Bash script, are saved as custom data on the event.

## Send events from any language {#any-language}

Any language that can make an HTTPS request can send events:

1. Build the event as JSON. [Event format](/docs/reference/event-format/) lists every field and the reserved data keys,
   such as `@simple_error` for errors and `@user` for the user.
2. `POST` an array of events to the events URL with these headers:
   - `Authorization: Bearer YOUR_API_KEY`
   - `Content-Type: application/json`
   - `User-Agent: your-client-name/1.0.0`, so the server records which client sent the event.
3. Check for `202 Accepted`. See [Posting events](/docs/api/posting-events/#responses) for the other responses.

These examples report a caught error and only use the standard library. They read the key from the
`EXCEPTIONLESS_API_KEY` environment variable.

```python tab="Python"
# Python 3.10 or later
import json
import os
import traceback
import urllib.request
from datetime import datetime, timezone

EVENTS_URL = "https://api.exceptionless.io/api/v2/events"


def submit(events):
    request = urllib.request.Request(
        EVENTS_URL,
        data=json.dumps(events).encode("utf-8"),
        method="POST",
        headers={
            "Authorization": f"Bearer {os.environ['EXCEPTIONLESS_API_KEY']}",
            "Content-Type": "application/json",
            "User-Agent": "my-python-reporter/1.0.0",
        },
    )
    with urllib.request.urlopen(request, timeout=10) as response:
        return response.status


def report_exception(error):
    return submit([{
        "type": "error",
        "date": datetime.now(timezone.utc).isoformat(),
        "data": {
            "@simple_error": {
                "message": str(error),
                "type": type(error).__name__,
                "stack_trace": "".join(traceback.format_exception(error)),
            },
        },
    }])


try:
    1 / 0
except ZeroDivisionError as error:
    print(report_exception(error))  # 202
```

```go tab="Go"
package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"os"
	"runtime"
	"strings"
	"time"
)

const eventsURL = "https://api.exceptionless.io/api/v2/events"

type event struct {
	Type    string         `json:"type"`
	Message string         `json:"message,omitempty"`
	Date    time.Time      `json:"date"`
	Data    map[string]any `json:"data,omitempty"`
}

func submit(events []event) (int, error) {
	body, err := json.Marshal(events)
	if err != nil {
		return 0, err
	}

	req, err := http.NewRequest(http.MethodPost, eventsURL, bytes.NewReader(body))
	if err != nil {
		return 0, err
	}
	req.Header.Set("Authorization", "Bearer "+os.Getenv("EXCEPTIONLESS_API_KEY"))
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("User-Agent", "my-go-reporter/1.0.0")

	client := &http.Client{Timeout: 10 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		return 0, err
	}
	defer resp.Body.Close()
	return resp.StatusCode, nil
}

// stackTrace lists function, file, and line for each caller. Unlike debug.Stack, it has no
// goroutine IDs or argument values, so the same error from the same place stacks together.
func stackTrace() string {
	pcs := make([]uintptr, 32)
	frames := runtime.CallersFrames(pcs[:runtime.Callers(2, pcs)])
	var sb strings.Builder
	for {
		frame, more := frames.Next()
		fmt.Fprintf(&sb, "%s\n\t%s:%d\n", frame.Function, frame.File, frame.Line)
		if !more {
			return sb.String()
		}
	}
}

func reportError(err error) (int, error) {
	return submit([]event{{
		Type: "error",
		Date: time.Now(),
		Data: map[string]any{
			"@simple_error": map[string]string{
				"message":     err.Error(),
				"type":        fmt.Sprintf("%T", err),
				"stack_trace": stackTrace(),
			},
		},
	}})
}

func main() {
	status, err := reportError(errors.New("payment provider timed out"))
	fmt.Println(status, err) // 202 <nil>
}
```

Exceptionless groups `@simple_error` events into stacks by the error `type` and the stack trace, so the same error from
the same place lands in one stack.

## Build a client library {#build-a-client}

A one-off request is enough for scripts. For an app, you'll want what the official clients do: queue events and send
them in the background, retry when the network is down, back off when the server says so, merge duplicates, apply the
project's data exclusions and log levels, and track sessions. The [client specification](/docs/reference/client-spec/)
describes each of these, with the endpoints and status codes to handle.

If you build a client for a new language, tell us in the
[Exceptionless repository](https://github.com/exceptionless/Exceptionless/issues).

## Ask Exie

```prompt exie
Did the Billing project receive any events in the last 10 minutes?
```

```prompt exie
Where do I find the setup steps for sending events to the Billing project?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can wire it up:

```prompt agent
Add Exceptionless error reporting to this service using the REST API at https://api.exceptionless.io/api/v2/events, following https://exceptionless.com/docs/clients/custom-clients/. Read the API key from the EXCEPTIONLESS_API_KEY environment variable, send errors as @simple_error, and never let a failed report crash the app. Then check with Exceptionless that the test error arrived.
```
