---
title: "JavaScript source maps"
navTitle: "Source maps"
metaTitle: "JavaScript source maps for stack traces"
description: "Get readable stack traces for minified JavaScript: let Exceptionless discover public source maps, or upload private ones from CI/CD with a scoped token."
order: 5
redirectFrom:
  - /docs/source-maps/
sources:
  - src/Exceptionless.Core/Services/SourceMaps/SourceMapService.cs
  - src/Exceptionless.Core/Services/SourceMaps/SourceMapDownloader.cs
  - src/Exceptionless.Core/Services/SourceMaps/SourceMapDocument.cs
  - src/Exceptionless.Core/Services/SourceMaps/SourceMapRequestThrottle.cs
  - src/Exceptionless.Core/Configuration/SourceMapOptions.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/15_SourceMapPlugin.cs
  - src/Exceptionless.Core/Bootstrapper.cs
  - src/Exceptionless.Web/Api/Endpoints/SourceMapEndpoints.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/source-maps/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/api-keys/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/stack-trace/source-map-status.svelte
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserErrorPlugin.ts
  - Exceptionless.JavaScript:packages/node/README.md
---

# JavaScript source maps

Production JavaScript is bundled and minified, so a stack frame such as `loadCurrentUser` in `src/user.ts` becomes `a`
in `app.a1b2c3.js` at line 1. Exceptionless uses your source maps to restore the original function names, files, lines,
and columns before it groups a new error into a stack. You get readable stack traces, and errors group by the code that
actually failed.

There are two ways to give Exceptionless your source maps:

| Your source maps are...                   | Do this                                                                                             |
| ----------------------------------------- | --------------------------------------------------------------------------------------------------- |
| Deployed publicly next to your JavaScript | [Nothing](#use-public-source-maps). Exceptionless finds them automatically.                         |
| Private, or kept only as build artifacts  | [Upload them](#upload-private-source-maps), by hand or [from CI/CD](#upload-source-maps-from-cicd). |

Source maps apply to new events only. Adding a map doesn't change events that were already processed.

They apply to stack frames whose file is an absolute HTTP or HTTPS URL and that have a line and column, which is what
the browser client reports. Node.js stack frames are file paths, so for Node.js run your app with
`node --enable-source-maps`, and Node.js maps the stack trace before the client sends it.

## Generate source maps

Have your bundler write a `.map` file for each production bundle:

- **Vite**: set `build.sourcemap` to `true`, or to `"hidden"` to leave out the `sourceMappingURL` comment.
- **webpack**: set `devtool` to `"source-map"`, or to `"hidden-source-map"` to leave out the comment.
- **esbuild**: pass `--sourcemap`.

Generate the maps in the same build that produces the JavaScript you deploy. A map from a different build points at the
wrong code.

## Use public source maps {#use-public-source-maps}

If your `.map` files are deployed with your JavaScript, there's nothing to configure. When a new error has a frame from
a file Exceptionless hasn't seen, it:

1. uses a map already stored for that exact file URL, if there is one;
2. otherwise downloads the generated JavaScript file over HTTPS;
3. looks for a `SourceMap` or `X-SourceMap` response header, then a `sourceMappingURL` comment in the file;
4. if neither is there, tries the file's URL with `.map` added;
5. validates and stores the map for the project, then maps the frame.

For example, these two files work with no setup:

```text
https://cdn.example.com/assets/app.a1b2c3.js
https://cdn.example.com/assets/app.a1b2c3.js.map
```

The `sourceMappingURL` comment can be relative, as bundlers usually write it, or absolute, or an inline `data:` URL.

Discovered maps appear on the project's **Source Maps** page as **Automatic**. When a discovered map is used and is more
than an hour old, Exceptionless downloads it again, so it follows a file that changed at the same URL. Content-hashed
file names such as `app.a1b2c3.js` avoid that problem entirely, because each URL always means one build.

### Requirements for discovery

- The JavaScript URL in the stack frame is an absolute `https://` URL on the default port (443).
- The JavaScript file and map are reachable from the internet without cookies, authentication, or an IP allowlist.
  Exceptionless doesn't connect to private network addresses, and follows at most 3 redirects.
- The map is a version 3 source map. Indexed maps, which have a `sections` property, aren't supported.
- The map is at most 20 MiB, and each download finishes within 3 seconds.

Exceptionless discovers a limited number of new maps for each project in a 15-minute period: 10 for organizations on the
Free plan and 50 on paid plans, with lower limits for each client API key. An event processed while the limit is reached
keeps its minified frames, and later events use the map once it's stored. When a download fails, Exceptionless doesn't
try that file again for 15 minutes.

Check that a deployed map is public before you rely on it:

```bash
curl --fail --silent --show-error --output /dev/null "https://cdn.example.com/assets/app.a1b2c3.js.map"
```

Public maps often include your original source code in `sourcesContent`. If that code shouldn't be public, don't deploy
the maps. Upload them instead.

## Upload private source maps {#upload-private-source-maps}

An uploaded map doesn't need to be public. Exceptionless stores it for the exact JavaScript URL you give it, and uses it
without downloading anything.

To upload a map by hand:

1. Open your project's settings and select **Source Maps**.
2. Under **Upload Source Map**, enter the **Generated JavaScript URL**: the full URL of the JavaScript file, exactly as
   it appears in the stack trace.
3. Choose the matching `.map` file under **Source map**, and select **Upload**.

The URL can be HTTP or HTTPS, on any host or port, but can't contain a user name, password, or `#` fragment. Keep the
path and query string exactly as they appear in the stack frame. A map can be up to 20 MiB.

Uploading another map for the same URL replaces the old one. The **Available Source Maps** table lists uploaded and
discovered maps with their size, when they were added, and when they were last used. Delete a map there with its trash
button.

## Upload source maps from CI/CD {#upload-source-maps-from-cicd}

Upload maps from the same build you deploy: build once, upload the maps, then deploy those exact files.

### 1. Create a source map upload token

1. Open your project's settings and select **API Keys**.
2. Select **Add token**, then **Source map upload token**.
3. Copy the new token and save it in your CI/CD system as a secret, for example `EXCEPTIONLESS_SOURCE_MAP_TOKEN`.

This token can only upload source maps, and only to this project. It can't read events, change settings, or send events.
Don't use the client API key from your app; it can't upload maps.

Your pipeline also needs your project's ID. It's in the address bar when you open the project in Exceptionless:
`/project/<project-id>/...`.

### 2. Upload each map

Send each map as `multipart/form-data` with two fields: `generated_file_url`, the public URL of its JavaScript file, and
`file`, the map:

```bash
curl --fail-with-body --request POST \
  "https://api.exceptionless.io/api/v2/projects/${EXCEPTIONLESS_PROJECT_ID}/source-maps" \
  --header "Authorization: Bearer ${EXCEPTIONLESS_SOURCE_MAP_TOKEN}" \
  --form-string "generated_file_url=https://cdn.example.com/assets/app.a1b2c3.js" \
  --form "file=@dist/assets/app.a1b2c3.js.map;type=application/json"
```

A successful upload returns `201 Created` with the stored map's details. An invalid URL or map returns
`422 Unprocessable Entity` with the reason, and so does an upload past your project's storage limits.

Run the request after the production build and before the release step, once for each `.map` file the build produced.
How you list the files and build their public URLs depends on your bundler and where you deploy.

> [!NOTE]
> Self-hosted instances use your own server URL in place of `https://api.exceptionless.io`.

## Verify

1. Trigger a new error in the deployed app.
2. Open the event. Its stack trace shows the original file and function names.
3. Open your project's settings and select **Source Maps**. The JavaScript URL is listed as **Automatic** or
   **Uploaded**. Usage is saved in the background, so **Last used** can take a while to show a date.

## Troubleshooting

When Exceptionless can't map a stack trace, the event's stack trace shows a **Source map unavailable** or **Stack trace
partially symbolicated** button. Select it to see each file that failed and why:

| Reason shown                                             | What to check                                                                                                |
| -------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| No usable source map could be downloaded.                | The map isn't public, isn't at a URL Exceptionless looks for, or the file isn't on HTTPS. Upload it instead. |
| The downloaded source map is invalid or unsupported.     | The file isn't a version 3 source map, or it's an indexed map with `sections`.                               |
| The source map does not contain this generated location. | The map came from a different build than the deployed JavaScript.                                            |
| The source map download timed out.                       | The map took longer than 3 seconds to download. Upload it instead.                                           |
| The source map could not be downloaded.                  | The server returned an error, a limit was reached, or the download was too large.                            |

If an uploaded map isn't used:

- Compare its **Generated file** URL with the file URL in the event's stack frame. They must match, including the query
  string.
- Send a new error. Existing events are not reprocessed.
- Confirm that the frame has a line and a column.

## Storage and retention

| Limit                                         | Free plan | Paid plans |
| --------------------------------------------- | --------- | ---------- |
| Source maps per project                       | 100       | 1,000      |
| Storage per project                           | 100 MiB   | 1 GiB      |
| Unused maps are deleted after                 | 14 days   | 90 days    |
| New maps discovered per project in 15 minutes | 10        | 50         |

A map counts as used when it maps a frame. A map that's never used is deleted that long after it was added. Automatic
discovery never rejects an event: if a map is unavailable or a limit is reached, the event keeps its original frames.

Self-hosted administrators can change these limits in the `SourceMaps` configuration section, and turn off discovery
with `EX_SourceMaps__EnableAutoDownload=false`. See [Self-hosting configuration](/docs/self-hosting/configuration/).

## Ask Exie

```prompt exie
Show me the stack trace of the latest event in this stack.
```

```prompt exie
Which error stacks are new this week?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can read the mapped stack trace and go straight
to the code:

```prompt agent
Get the latest event for the top JavaScript error stack in my Exceptionless project, use its source-mapped stack trace to find the failing code in this repository, and fix it.
```
