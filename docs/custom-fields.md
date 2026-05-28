# Custom Fields Architecture

Custom fields let organizations index arbitrary event data properties for use in filters and search. This document covers the full lifecycle, slot system internals, deletion policy, and operator support.

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

> **Note on `string` cost**: Each `string` slot creates **two** Elasticsearch field mappers (the `text` field and its `.keyword` sub-field), making it twice as expensive as other types toward Elasticsearch's `index.mapping.total_fields.limit` (default 1,000).

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

Two system fields are provisioned automatically per organization and are **protected from deletion**:

| Field Name | Type | Slot | Purpose |
|-----------|------|------|---------|
| `sessionend` | `date` | `date-1` | Session end timestamp (session tracking) |
| `haserror` | `bool` | `bool-1` | Whether the session has an associated error |

Because system fields are provisioned via `EnsureSystemFieldsAsync` **before** any user-defined fields, they always occupy slot 1 of their type. User fields for `date` start at slot 2; user fields for `bool` start at slot 2.

If `EnsureSystemFieldsAsync` is not called (e.g., legacy org before custom fields were introduced), the first user to create a `date` or `bool` field would accidentally claim slot 1. The controller calls `EnsureSystemFieldsAsync` before every field creation to prevent this.

### Slot Exhaustion and Elasticsearch Field Limits

Elasticsearch's default `index.mapping.total_fields.limit` is **1,000 field mappers**. Physical slot fields are only created in the index mapping the first time a document with that slot is indexed. The maximum Elasticsearch fields from custom fields is bounded by the highest slot number ever used, multiplied by number of types, multiplied by 2 (for `string` types).

With a hard limit of 20 active fields per organization and slot recycling deferred beyond the retention window, slot numbers grow slowly over time. For a typical organization cycling through fields over years, the slot high-water mark remains very low (well under 100 per type). Across many organizations sharing the same physical ES index, the absolute maximum slot number approaches the highest number ever assigned to any organization — still bounded in practice.

**Field churn analysis**: The worst-case scenario is an organization that continuously creates and deletes the maximum 20 fields. Each create/delete cycle permanently consumes one slot. For any single `(EntityType, TenantKey, IndexType)` combination, the slot high-water mark is bounded by the number of distinct fields ever created. Across 8 types and 20 active fields with unlimited churn, the theoretical maximum is `8 types × unlimited cycles` — but since fields created in Elasticsearch are shared across all tenants in the index, the practical concern is the *total* unique slot number across all active organizations, not per-organization. The active per-organization cap of 20 limits how many fields a single organization can consume in any given period.

There is no application-level per-type slot ceiling — the framework relies on Elasticsearch's mapping limit (default 1,000 field mappers) as the ultimate guard. A future retention-aware cleanup job will hard-delete definitions and free slots after all events indexed with those slots have aged out.

## Field Lifecycle

### Creating a Field

1. User calls `POST /organizations/{id}/event-custom-fields`
2. API validates: premium plan check, reserved name check, active quota check, duplicate name check
3. `EnsureSystemFieldsAsync` provisions `sessionend` and `haserror` if not yet present
4. `AddFieldAsync` assigns the next available slot and persists the definition
5. **From this point on, new events with matching data keys are indexed into the slot**
6. **Existing events are NOT backfilled** — they retain their original `Idx` content unchanged

> **Search semantics on creation**: Custom field indexing applies only to events processed **after** the field definition is created. Historical events are not re-indexed. If you need historical data, you must re-ingest events or use data-level queries (`data.fieldname:value`) instead of slot queries.

### Updating a Field

Only `Description` and `DisplayOrder` are mutable. `Name`, `IndexType`, and `IndexSlot` are immutable once created (enforced by Foundatio's repository at save time).

### Deleting a Field

Deletion is a two-phase process designed to prevent **slot reuse corruption** — where a recycled slot causes historical events for a deleted field to appear in queries for a new field with the same slot.

**Phase 1 — Soft Delete (synchronous):**
1. API checks for usage in saved view filters — returns 409 Conflict if found
2. API marks `IsDeleted = true` and calls `SaveAsync`
3. The field name is freed from the slot system (a new field can use the same name)
4. The slot number is **not** freed — it remains occupied
5. New events no longer index data into this slot
6. API returns 200 OK; the field disappears from the management UI
7. A `RemoveCustomFieldWorkItem` is enqueued

**Phase 2 — Slot Cleanup (deferred):**
The `RemoveCustomFieldWorkItemHandler` currently **acknowledges** the soft-delete without hard-deleting the definition record. This is intentional:

> **Slot Reuse Safety**: If a slot is freed and immediately recycled for a new field, historical events within the retention window that had data for the old field will appear in queries for the new field. For example: delete "customer_id" (keyword-3), create "project_id" (gets keyword-3), then searching `project_id:acme` returns historical events where `customer_id` was `acme`. This is a data integrity violation.

Hard-delete (slot freeing) is deferred to a **future retention-aware cleanup job** that will only reclaim slots after all events indexed with that slot have aged out of the retention window. Until then, the slot number grows monotonically but is never reused for a different field.

> **Search semantics on deletion**: After Phase 1, no new events write to the deleted slot. Existing events indexed with this field remain searchable via the slot path until they age out per the organization's retention policy. After soft-deletion and within the retention window, you may still get results from historical events if you query by slot path directly — the management UI and query builder will not surface the deleted field, so this is only visible via raw slot queries.

### Slot Recycling

Slot recycling (reusing a freed slot number for a new field) is **currently deferred** to prevent data contamination within the retention window. See "Deleting a Field" above. A future cleanup job will safely reclaim slots after the retention period expires.

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
- Not system fields (`sessionend`, `haserror`)

Soft-deleted fields awaiting cleanup do **not** count toward the active quota. A user who has 20 active fields can delete some and immediately create replacements — the quota check reflects the current active state.

### Deletion Blocked by Saved Views

If a custom field is referenced in any saved view filter for the organization, deletion is blocked with HTTP 409 Conflict. The filter is checked using a regex that matches `idx.{fieldName}` tokens in the filter string. Users must remove the field from all saved view filters before deletion proceeds.

## Plan Restrictions

Custom fields require a paid plan. Organizations on the free plan receive HTTP 426 Upgrade Required when attempting to create a custom field. Existing fields are unaffected if an organization downgrades — they remain indexed but the management UI is read-only.

## Security Model

- All custom field API endpoints require authentication and verify organization ownership before any operation
- Field names are validated against a strict allowlist (`[a-zA-Z0-9_.\-]`, max 100 chars, no `@` prefix)
- Names starting with `@` are reserved for Exceptionless internal data keys (`@error`, `@request`, etc.)
- Users cannot access or modify custom fields belonging to other organizations (tenant isolation enforced at the controller layer)
- System fields (`sessionend`, `haserror`) cannot be created or deleted via the API

## Elasticsearch Mapping Considerations

- Custom field slot templates are registered via `AddStandardCustomFieldTypes()` in `EventIndex`
- Templates use the pattern `idx.{type}-*` (e.g., `idx.keyword-*`, `idx.double-*`)
- Elasticsearch creates field mappings dynamically on first document write — unused slots have zero mapping cost
- Monitor total field count relative to `index.mapping.total_fields.limit` (default 1,000) in high-volume deployments
- The `string` type creates 2 field mappers per slot; all other types create 1 field mapper per slot

## Common Questions

**Can I reuse a field name after deleting it?**
Yes, immediately. After soft-deletion, the field name is freed and can be used for a new field. The new field gets a **new** slot number (not the old one), which prevents historical events for the deleted field from appearing in queries for the new field. Slot numbers grow monotonically and are not recycled until a future retention-aware cleanup job runs.

**Does the 20-field quota include soft-deleted fields?**
No. The quota counts only *active* (non-deleted, non-system) fields. Soft-deleted fields awaiting cleanup are excluded. You can delete fields and immediately create replacements up to the quota.

**Will deleting a field break existing queries?**
Saved view filters that reference the field are blocked at deletion time. Custom code that queries `idx.keyword-N:value` directly may stop returning results as events age out, but this is expected behavior. The Exceptionless query builder translates field names to slot paths automatically; raw slot queries are not recommended.

**Is there a per-type field limit?**
No. The active quota (`MaxFieldsPerOrganization = 20`) is a total across all types. There is no separate limit per type.

**What happens if I downgrade my plan?**
Existing field definitions and indexed data are preserved. The custom fields management UI becomes read-only. New field creation requires re-upgrading.

**Can I have more than 20 fields?**
The default limit is 20 per organization. This can be increased via the `MaxFieldsPerOrganization` configuration key for self-hosted deployments.

**Can slot numbers grow unboundedly from field churn?**
In theory, yes — each delete-then-recreate cycle adds one slot number that is never recycled. In practice, each cycle only costs one ES field mapper, and a field-churning organization (20 create/delete cycles per type) would accumulate at most ~160 slot numbers. Elasticsearch's mapping limit of 1,000 fields per index is the ultimate safety boundary. The future retention-aware cleanup job will reclaim slots and reset slot growth for organizations that heavily churn fields.
