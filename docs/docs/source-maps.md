---
title: "JavaScript Source Maps"
---

# JavaScript Source Maps

Exceptionless uses source maps to turn minified JavaScript stack frames into the original file names, line and column numbers, and function names. Symbolication happens before an event is assigned to a stack, so readable function names also improve stack grouping.

## Automatic discovery

No domain allowlist or project setup is required for public source maps. When an error contains an absolute HTTPS JavaScript URL, Exceptionless checks the generated file's `SourceMap` or `X-SourceMap` response header and its `sourceMappingURL` comment. If neither is present, it also checks the conventional `<generated-file>.map` URL.

Downloaded maps are validated and cached in project-scoped file storage. Automatically downloaded maps are revalidated after one hour so a stable generated-file URL cannot retain a map from an older deployment indefinitely. If refresh fails, Exceptionless leaves the generated frame unchanged instead of risking a misleading stack trace from the stale map.

Exceptionless only makes anonymous HTTPS requests on the standard port to public network addresses. Redirects and every resolved address are revalidated. New automatic discoveries use plan-aware limits per client key, project, and organization; outbound requests are also limited per destination, IP address, and cluster. Free plans allow five new discoveries per client key and ten per project or organization in each 15-minute window by default. Paid plans have higher limits. Failed URLs are cached for 15 minutes, duplicate discoveries share the same in-flight work, and refreshes use a separate smaller outbound budget. If any limit is reached, Exceptionless leaves the generated frame unchanged without rejecting or delaying the event.

Downloads also have time, redirect, size, and local and cluster-wide concurrency limits. Parsed maps use a bounded in-memory cache. Manual and deployment uploads do not consume automatic-discovery quotas. Each project can store up to 1,000 maps and 1 GiB by default; replacing a map for the same generated URL does not consume another slot. Self-hosted installations can tune these safeguards under the `SourceMaps` configuration section, including `MaximumArtifactsPerProject`, `MaximumStorageSizePerProject`, `AutoDownloadRateLimitPeriodMinutes`, `MaximumAutoDiscoveriesPerFreeClientKey`, `MaximumAutoDiscoveriesPerProject`, `MaximumAutoDownloadRequestsPerDestination`, `MaximumAutoRefreshRequestsGlobally`, `AutoDownloadRefreshIntervalMinutes`, `ParsedSourceMapCacheLifetimeMinutes`, and `MaximumParsedSourceMapCacheSize`.

## Uploading a source map

Upload a map when it is private or is not deployed next to the generated JavaScript:

1. Open the project and select **Source Maps** under **Project Settings**.
2. Enter the exact absolute URL that appears in the generated stack frame, including any path or query string used to identify the build.
3. Select the corresponding source map and upload it.

Uploading another map for the same generated file URL replaces the previous map. Uploaded and automatically discovered maps appear together on the Source Maps page and can be deleted there.

### Uploading during deployment

For build automation, create a project-scoped token that has only the `source-maps:write` scope. Set `EXCEPTIONLESS_SERVER_URL` to `https://be.exceptionless.io` for the hosted service or to the root URL of your self-hosted installation. Create the token once with a user-scoped token, then store the returned `id` as a protected CI/CD secret:

```shell
curl --fail-with-body --request POST \
  "${EXCEPTIONLESS_SERVER_URL}/api/v2/projects/${EXCEPTIONLESS_PROJECT_ID}/tokens" \
  --header "Authorization: Bearer ${EXCEPTIONLESS_USER_TOKEN}" \
  --header "Content-Type: application/json" \
  --data "{\"organization_id\":\"${EXCEPTIONLESS_ORGANIZATION_ID}\",\"project_id\":\"${EXCEPTIONLESS_PROJECT_ID}\",\"scopes\":[\"source-maps:write\"],\"notes\":\"CI source map uploads\"}"
```

Upload each map after the generated JavaScript has been deployed. The `generated_file_url` must be the exact absolute URL that will appear in stack frames:

```shell
curl --fail-with-body --request POST \
  "${EXCEPTIONLESS_SERVER_URL}/api/v2/projects/${EXCEPTIONLESS_PROJECT_ID}/source-maps" \
  --header "Authorization: Bearer ${EXCEPTIONLESS_SOURCE_MAP_TOKEN}" \
  --form "generated_file_url=https://cdn.example.com/assets/app.a1b2c3.js" \
  --form "file=@dist/assets/app.a1b2c3.js.map;type=application/json"
```

The upload token is accepted only for its assigned project and cannot read events, manage the project, list source maps, or use ordinary user APIs. Do not use a normal client API key for source map uploads because client keys are commonly embedded in distributed applications.

Source maps must use the version 3 flat-map format. Indexed source maps with a `sections` property and authenticated automatic downloads are planned follow-up capabilities; private maps can be uploaded in the meantime.

## Deployment guidance

Generate source maps as part of the same build that produces the minified JavaScript. Content-hashed generated file names are preferred because the generated URL then identifies a specific build. You can publish the `.map` file for zero-configuration discovery or keep it private and upload it to Exceptionless during deployment.
