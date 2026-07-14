---
title: "JavaScript Source Maps"
---

# JavaScript Source Maps

Exceptionless uses source maps to turn minified JavaScript stack frames into the original file names, line and column numbers, and function names. Symbolication happens before an event is assigned to a stack, so readable function names also improve stack grouping.

## Automatic discovery

No domain allowlist or project setup is required for public source maps. When an error contains an absolute HTTPS JavaScript URL, Exceptionless checks the generated file's `SourceMap` or `X-SourceMap` response header and its `sourceMappingURL` comment. If neither is present, it also checks the conventional `<generated-file>.map` URL.

Downloaded maps are validated and cached in project-scoped file storage. Automatically downloaded maps are revalidated after one hour so a stable generated-file URL cannot retain a map from an older deployment indefinitely. If refresh fails, Exceptionless leaves the generated frame unchanged instead of risking a misleading stack trace from the stale map.

Exceptionless only makes anonymous HTTPS requests to public network addresses. Redirects are revalidated, and downloads have time, redirect, size, concurrency, and per-project rate limits. Parsed maps use a bounded in-memory cache. Self-hosted installations can tune these safeguards under the `SourceMaps` configuration section, including `AutoDownloadRefreshIntervalMinutes`, `ParsedSourceMapCacheLifetimeMinutes`, and `MaximumParsedSourceMapCacheSize`.

## Uploading a source map

Upload a map when it is private or is not deployed next to the generated JavaScript:

1. Open the project and select **Source Maps** under **Project Settings**.
2. Enter the exact absolute URL that appears in the generated stack frame, including any path or query string used to identify the build.
3. Select the corresponding source map and upload it.

Uploading another map for the same generated file URL replaces the previous map. Uploaded and automatically discovered maps appear together on the Source Maps page and can be deleted there.

Source maps must use the version 3 flat-map format. Indexed source maps with a `sections` property and authenticated automatic downloads are planned follow-up capabilities; private maps can be uploaded in the meantime.

## Deployment guidance

Generate source maps as part of the same build that produces the minified JavaScript. Content-hashed generated file names are preferred because the generated URL then identifies a specific build. You can publish the `.map` file for zero-configuration discovery or keep it private and upload it to Exceptionless during deployment.
