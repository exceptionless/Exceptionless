# Custom Fields Architecture

Custom fields let organizations explicitly select event data properties to index for use in filters and search. Indexing is forward-only: creating a definition affects new events and never mutates or reindexes historical events. This document covers the full lifecycle, slot system internals, deletion policy, and operator support.

## Overview

When an event is processed, the pipeline handler inspects the event's `Data` dictionary and writes typed values into the `Idx` sub-document using a **pooled slot** model. Rather than creating a unique Elasticsearch field per organization per field name (which would cause mapping explosion in a multi-tenant index), all organizations share a small pool of physical ES fields like `idx.keyword-1`, `idx.double-2`, etc. Each organization gets its own independent slot namespace: "department" for Org A and "department" for Org B both map to `idx.keyword-1` but are isolated by tenant-scoped queries.

### Foundatio Integration

The custom fields system is built on [Foundatio.Repositories.Elasticsearch custom fields](https://repositories.foundatio.dev/guide/custom-fields). Key components:

- `IHaveVirtualCustomFields` — implemented by `PersistentEvent` to control how field values are read/written
- `ICustomFieldDefinitionRepository` — stores field definitions with slot assignments per `(EntityType, TenantKey, IndexType)`
- `EventCustomFieldService` — wires the document-changing pipeline hook and handles system field provisioning
- `EventIndex` — registers the 8 standard custom field types via `AddStandardCustomFieldTypes()`

### Supported Field Types

| Type | ES Mapping | Physical Slot Pattern | Filter Operators |
|------|-----------|----------------------|-----------------|
| `keyword` | Keyword (exact match) | `idx.keyword-{n}` | equals, not-equals, exists, missing |
| `string` | Text + `.keyword` sub-field | `idx.string-{n}` | contains/search, exists, missing |
| `int` | Integer | `idx.int-{n}` | equals, gt, gte, lt, lte, range, exists, missing |
| `long` | Long | `idx.long-{n}` | equals, gt, gte, lt, lte, range, exists, missing |
| `double` | Double | `idx.double-{n}` | equals, gt, gte, lt, lte, range, exists, missing |
| `float` | Float | `idx.float-{n}` | equals, gt, gte, lt, lte, range, exists, missing |
| `bool` | Boolean | `idx.bool-{n}` | true, false, exists, missing |
| `date` | Date | `idx.date-{n}` | equals, range, gt, gte, lt, lte, exists, missing |

> **Note on `string` cost**: Each `string` slot creates **two** Elasticsearch field mappers (the `text` field and its `.keyword` sub-field), making it twice as expensive as other types toward Elasticsearch's `index.mapping.total_fields.limit` (Exceptionless default 1,500).

### Choosing a Field Type

The configured type controls only the indexed representation in `Idx`; the original value in the event's `Data` dictionary is not changed. Use `keyword` for identifiers and versions whose exact text matters, even when they look numeric. Use `double` only when numeric range comparisons are intended.

For example, a `DatabaseVersion` value of `"4.90"` is indexed as the exact string `"4.90"` when configured as `keyword`, but as the number `4.9` when configured as `double`. A development value such as `"4.90 build 1234 30-Aug-2024"` is valid as a `keyword` and is skipped as a `double` because it cannot be converted. In every case, the original `Data["DatabaseVersion"]` value remains unchanged.

## Slot System

### How Slots Are Assigned

Slots are assigned **sequentially** per `(EntityType, TenantKey, IndexType)` scope:

```
Org A: "department"  → keyword slot 1  → idx.keyword-1
Org A: "region"      → keyword slot 2  → idx.keyword-2
Org B: "department"  → keyword slot 1  → idx.keyword-1  ← same physical field, different tenant
Org B: "priority"    → int slot 1      → idx.int-1
```

Slot assignment is protected by a **distributed lock** per scope to prevent duplicate allocation under concurrent writes.

### System Fields

Three system fields are provisioned automatically per organization and are **protected from deletion**:

| Field Name | Type | Slot | Purpose |
|-----------|------|------|---------|
| `sessionend` | `date` | `date-1` | Session end timestamp (session tracking) |
| `haserror` | `bool` | `bool-1` | Whether the session has an associated error |
| `@ref:session` | `keyword` | `keyword-1` | Session reference identifier |

`EnsureSystemFieldsAsync` provisions these definitions before user-defined fields and verifies that each reserved name has the expected type and slot. It fails with a deterministic conflict if a reserved definition is duplicated, soft-deleted, assigned to the wrong slot or type, or if another definition already occupies a reserved slot. This prevents silently writing data to one slot while query resolution reads another.

Legacy event documents can still contain the pre-pooled fields `idx.session-r`, `idx.sessionend-d`, and `idx.haserror-b`. Session filters expand across both the current pooled slot and the corresponding legacy field. Positive, range, and exists expressions use `OR`; missing expressions use `AND`; and negation wraps the combined expression.

### Slot Exhaustion and Elasticsearch Field Limits

Exceptionless configures `index.mapping.total_fields.limit` from `Elasticsearch:FieldsLimit`, which defaults to **1,500 field mappers** (Elasticsearch itself defaults to 1,000). Physical slot fields are only created in the index mapping the first time a document with that slot is indexed. The maximum Elasticsearch fields from custom fields is bounded by the highest slot number ever used, multiplied by number of types, multiplied by 2 (for `string` types).

Two limits bound allocation per organization. `MaxFieldsPerOrganization` limits active user definitions, and `MaxLifetimeFieldsPerOrganization` limits all user definitions ever allocated, including soft-deleted definitions. Both default to 20; system fields are excluded. Existing organizations already above a reduced limit remain readable and indexable, but cannot allocate another slot.

Retention-aware hard deletion and slot reclamation are not implemented. Deleted definitions continue to reserve their slots, and the lifetime ceiling prevents create/delete churn from growing the slot high-water mark without bound.

## Field Lifecycle

### Creating a Field

1. User calls `POST /organizations/{id}/event-custom-fields`
2. API validates: premium plan check, reserved name check, active quota check, duplicate name check
3. `EnsureSystemFieldsAsync` provisions `sessionend`, `haserror`, and `@ref:session` if they are not yet present
4. `AddFieldAsync` assigns the next available slot and persists the definition
5. **From this point on, new events with matching data keys are indexed into the slot**
6. **Existing events are NOT backfilled** — they retain their original `Idx` content unchanged

> **Search semantics on creation**: Custom field indexing applies only to events processed **after** the field definition is created. Historical events are not re-indexed. Both `data.fieldname:value` and `idx.fieldname:value` resolve to the new pooled slot, so neither expression searches a retained legacy named index. V1 has no built-in historical backfill. Elasticsearch reindexing alone is insufficient because it does not run the Exceptionless custom-field transform that populates pooled slots. Replaying original payloads can create duplicate events and requires an operator-owned deduplication plan.

### Upgrade Cutover from Automatic Extended-Data Indexing

Before this custom-field model, paid organizations automatically copied primitive extended-data values into named `Idx` fields and `data.fieldname:value` queries resolved to those legacy fields. This release intentionally replaces that behavior with explicit definitions:

- Unregistered extended-data keys are no longer indexed.
- Existing named `Idx` values remain physically unchanged but are not queried through a new custom-field definition.
- Creating a definition indexes only events processed after creation; there is no automatic backfill.
- The Exceptionless-owned session fields (`@ref:session`, `sessionend`, and `haserror`) are the narrow compatibility exception and continue to read both legacy and pooled storage during the retention window.

Before upgrading a self-hosted installation, inventory saved views and integrations that filter arbitrary `data.*` fields. After upgrading, create definitions for the fields that still matter before resuming ingestion when uninterrupted forward indexing is required. There is no supported general backfill in v1.

### Updating a Field

Only `Description` and `DisplayOrder` are mutable. `Name`, `IndexType`, and `IndexSlot` are immutable once created (enforced by Foundatio's repository at save time).

### Deleting a Field

Deletion is a synchronous soft-delete designed to prevent **slot reuse corruption** — where a recycled slot causes historical events for a deleted field to appear in queries for a new field with the same slot.

1. API checks for usage in saved view filters — returns 409 Conflict if found
2. API marks `IsDeleted = true` and calls `SaveAsync`
3. The field name is freed from the slot system (a new field can use the same name)
4. The slot number is **not** freed — it remains occupied
5. New events no longer index data into this slot
6. API returns 204 No Content; the field disappears from the management UI

> **Slot Reuse Safety**: If a slot is freed and immediately recycled for a new field, historical events within the retention window that had data for the old field will appear in queries for the new field. For example: delete "customer_id" (keyword-3), create "project_id" (gets keyword-3), then searching `project_id:acme` returns historical events where `customer_id` was `acme`. This is a data integrity violation.

Hard-delete (slot freeing) is deferred until retention-aware cleanup is implemented. Until then, the slot number grows monotonically and is never reused for a different field. Deleting the owning organization is the exception: organization teardown permanently removes all of its custom-field definitions because no tenant data remains eligible for future queries.

> **Search semantics on deletion**: After Phase 1, no new events write to the deleted slot and the field name no longer resolves in queries. Existing slot values remain in historical event documents until those events age out, but direct raw-slot queries are blocked by the repository query resolver.

### Slot Recycling

Slot recycling (reusing a freed slot number for a new field) is **currently deferred** to prevent data contamination within the retention window. See "Deleting a Field" above. Deleted definitions continue reserving their slots until retention-aware cleanup is implemented.

**Name reuse is always safe**: After soft-deletion, the same field *name* can be immediately reused. The new definition gets the *next available* slot number (monotonically increasing), not the old slot. This means:

```
Field A created  → keyword slot 1 (active)
Field B created  → keyword slot 2 (active)
Field A deleted  → soft-delete (slot 1 still occupied, name freed)
Field C created with same name → keyword slot 3 (new slot, no contamination)
Query for "Field C" → only returns events since Field C was created
```



The active field limit (`MaxFieldsPerOrganization`, default 20) counts only fields that are:
- Not soft-deleted (`IsDeleted = false`)
- Not system fields (`sessionend`, `haserror`, `@ref:session`)

Soft-deleted fields do **not** count toward the active quota, but they do count toward `MaxLifetimeFieldsPerOrganization` (default 20). A deleted field therefore does not guarantee that another slot can be allocated.

### Deletion Blocked by Saved Views

If a custom field is referenced in any saved view filter for the organization, deletion is blocked with HTTP 409 Conflict. References are taken from the canonical parsed query tree, so quoted values and escaped text do not create false positives. Users must remove the logical `idx.{fieldName}` or `data.{fieldName}` reference from all saved views before deletion proceeds.

## Plan Restrictions

Custom fields require a paid plan. Organizations on the free plan receive HTTP 426 Upgrade Required when attempting to create a custom field. Existing fields are unaffected if an organization downgrades — they remain indexed but the management UI is read-only.

## Security Model

- All custom field API endpoints require authentication and verify organization ownership before any operation
- Field names are validated against a strict allowlist (`[a-zA-Z0-9_.\-]`, max 100 chars, no `@` prefix)
- Names starting with `@` are reserved for Exceptionless internal data keys (`@error`, `@request`, etc.)
- Users cannot access or modify custom fields belonging to other organizations (tenant isolation is enforced by the API handler's organization-access checks)
- System fields (`@ref:session`, `sessionend`, `haserror`) cannot be created, modified, or deleted via the API

## Elasticsearch Mapping Considerations

- Custom field slot templates are registered via `AddStandardCustomFieldTypes()` in `EventIndex`
- Startup applies and validates all eight typed templates on every retained event index before readiness; legacy suffix templates remain during mixed-version rollout
- An incompatible existing typed-slot mapping is a rollout blocker that requires an explicit migration; Elasticsearch cannot safely change an existing field type in place
- Templates use the pattern `idx.{type}-*` (e.g., `idx.keyword-*`, `idx.double-*`)
- Elasticsearch creates field mappings dynamically on first document write — unused slots have zero mapping cost
- Monitor total field count relative to `index.mapping.total_fields.limit` (`Elasticsearch:FieldsLimit`, Exceptionless default 1,500) in high-volume deployments
- The `string` type creates 2 field mappers per slot; all other types create 1 field mapper per slot

## Common Questions

**Can I reuse a field name after deleting it?**
Yes, immediately. After soft-deletion, the field name is freed and can be used for a new field. The new field gets a **new** slot number (not the old one), which prevents historical events for the deleted field from appearing in queries for the new field. Slot numbers grow monotonically and are not recycled while retention-aware cleanup remains unimplemented.

**Does the 20-field quota include soft-deleted fields?**
The active quota does not. The lifetime quota does. With both defaults set to 20, a soft-deleted field frees active capacity but not lifetime slot capacity.

**Will deleting a field break existing queries?**
Saved view filters that reference the field are blocked at deletion time. Custom code cannot query `idx.keyword-N:value` directly because raw slot access is blocked. The Exceptionless query builder translates active field names to slot paths automatically.

**Is there a per-type field limit?**
No. The active quota (`MaxFieldsPerOrganization = 20`) is a total across all types. There is no separate limit per type.

**What happens if I downgrade my plan?**
Existing field definitions and indexed data are preserved. The custom fields management UI becomes read-only. New field creation requires re-upgrading.

**Can I have more than 20 fields?**
Both limits default to 20 per organization. Self-hosted deployments can raise `MaxFieldsPerOrganization` and `MaxLifetimeFieldsPerOrganization`; the lifetime limit must be greater than or equal to the active limit. When only the active key is specified, the lifetime limit defaults to that value.

**Can slot numbers grow unboundedly from field churn?**
No for a single organization under the default lifetime ceiling. Slot reclamation is still unavailable, so operators should monitor Elasticsearch total-field headroom across all organizations and retained daily indices.

## Production Rollout and Recovery

1. Deploy ingestion and API instances first. Every instance gates readiness on the shared schema-and-day marker; one distributed-lock holder installs and validates retained-index mappings, and waiting instances proceed only after that succeeds. Drain older writers before exposing typed writes.
2. Seed required definitions only after the backend fleet is current. Expose the management UI after exact, range, and facet canaries succeed.
3. Monitor the low-cardinality custom-field diagnostics for mapping/provision failures, conversion skips, lifetime-limit rejection, and Elasticsearch field-count headroom. Alert on any mapping or provisioning failure.
4. On failure, stop further rollout and definition mutations, preserve definitions and raw event data, fix forward, and repeat the canary. Never hard-delete definitions or reclaim slots during incident response.

The two-phase indexing transform builds replacement managed slots away from the document. New-event mapping failures preserve raw `Data` while stripping untrusted managed slots; saved-event infrastructure failures abort the write rather than persisting a de-indexed event.
