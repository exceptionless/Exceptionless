# Product Tour Persistence

This note records the persistence boundary for the Svelte product tours. Tour state is a small user preference, not a reporting schema.

## Scope

The supported shape is one `ProductTourState` record on `User` with seven nullable UTC dates:

- `app_overview`, `exie_overview`, `event_investigate`, `project_configure`, and `saved_view_create` record the first completion of a guide.
- `app_welcome` and `exie_announcement` record the first acknowledgment of an automatic invitation. Accepting and dismissing an invitation both acknowledge it.

The self-only `PUT /api/v2/users/me/product-tours/{tourName}/record` operation resolves an allowlisted stable record name on the server, records the date only when it is empty, and returns the authoritative timestamp. The client maps the existing UI IDs at its catalog boundary: `new-ui-overview` → `app-overview`, `meet-exie` → `exie-overview`, `investigate-error` → `event-investigate`, `configure-project` → `project-configure`, and `create-saved-view` → `saved-view-create`; `welcome` → `app-welcome` and the Exie announcement maps to `exie-announcement`. The server maps those record names to the typed fields. It must use the existing targeted repository patch and cache behavior. A missing user is not created, and a client cannot provide a timestamp, field path, status, or version.

The UI keeps its local active-tour checkpoint. Finishing or dismissing a guide, and accepting or dismissing an invitation, completes locally even if persistence or feature-usage telemetry fails. Guide dismissal does not record completion. Existing `product-tour.completed.*` and `product-tour.dismissed.*` feature-usage events remain action counts; no tour dashboard, aggregation endpoint, custom collector, or funnel is part of this feature.

The dates use the existing serializer (`snake_case_lower`, null omission) and remain in Elasticsearch `_source`. `UserIndex` stays dynamically unmapped for these fields. Do not add a mapping or reindex unless querying or aggregating tour dates becomes a separately approved requirement.

## Baseline and deployment evidence

The reviewed implementation baseline for PR #2506 is `f275caae14e91229e4484ffa685ca1d6244ba91b` on `feature/ui-guided-tours-review`, based on `origin/main` `5aeca2d898048cf0307495758e1234352e005379`.

The integration checkout initially pointed at `a057330fc12e91db92b0b25ce0df507880a03a29`, an earlier restored-tour snapshot, and therefore did not match the reviewed PR head. Changes made from that checkout must be reconciled and validated against `f275caae14e91229e4484ffa685ca1d6244ba91b` before they are treated as PR evidence.

The earlier guided-tour implementation was merged by PR #2458 at `b178420820d927d7a5d73699ced9f92e6ea4c70a` on 2026-08-20 13:18 UTC and reverted by PR #2505 at `074e97bf62d0f9c804155c732f48f4ab2326f0cf` on 2026-08-20 14:43 UTC. Both commits are ancestors of release tags `v8.8.1` and later. Repository history therefore shows no released version after the revert that contains the old `status`/`version` tour shape.

That history does not prove whether a deployment occurred during the short interval between the two merges. Before removing compatibility code, check the hosted deployment history for that interval. If no deployment used the old shape, replace it directly. If one did, document the observed records and choose the smallest explicit transition, preferably a deliberate reset of optional tour state rather than invented completion dates.

## Rollout and reset rules

Do not run an uncontrolled rolling deployment where an old server can read a newly written date-valued `product_tours` object. Coordinate the cutover or retain a tested compatibility window based on deployment evidence.

If a future release must show a tour again, use a one-time bounded user migration that explicitly removes the target date from the persisted document. A null-valued partial object is not sufficient because nulls are omitted by the serializer. Inspect source state, preserve later completions and unrelated user fields, and follow established cache migration conventions. Do not add runtime tour versions or a generic reset service.

## Verification recorded for this change

- `dotnet build Exceptionless.slnx --no-restore`: passed with 0 warnings and 0 errors.
- The full backend suite passed 2,960 of 2,963 tests with 3 documented skips (assistant evaluation, performance-data, and EventRepository performance tests); there were 0 failures.
- `ProductTourEndpointTests`: 10 passed against the isolated Elasticsearch test host, including duplicate timestamp, concurrent fields, cache freshness, invalid input, and deleted-user no-create coverage.
- `OpenApiSnapshotTests`: 4 passed; `EndpointManifestTests`: 1 passed. The generated client was regenerated from the resulting canonical OpenAPI snapshot.
- `UserSerializerTests`: 14 passed. The Svelte unit suite passed 834 tests across 108 files; `npm run validate` passed with 0 Svelte diagnostics, 0 warnings, and clean Prettier/ESLint checks; `npm run build` passed.

The isolated runtime used Elasticsearch on a task-owned local port and the existing local Redis service. The default shared Elasticsearch volume had an 8.19.21/8.19.15 image mismatch and was left untouched. Browser checks covered the rendered tour flows separately; deployment data presence for the old shape remains unknown and requires a hosted audit before rollout.

The final rendered evidence is `/private/tmp/exceptionless-tour-qa-final/welcome-desktop.png` and `/private/tmp/exceptionless-tour-qa-final/welcome-mobile.png`. Those checks cover the welcome surface at desktop and mobile sizes; the remaining spotlight and keyboard paths require separate browser evidence before claiming complete visual coverage.
