<script lang="ts">
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as Select from '$comp/ui/select';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Spinner } from '$comp/ui/spinner';
    import * as Table from '$comp/ui/table';
    import { Textarea } from '$comp/ui/textarea';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import {
        createCustomFieldMutation,
        type CustomFieldDefinition,
        deleteCustomFieldMutation,
        getCustomFieldsQuery,
        INDEX_TYPE_DESCRIPTIONS,
        INDEX_TYPE_LABELS,
        INDEX_TYPES,
        type IndexType,
        updateCustomFieldMutation
    } from '$features/organizations/custom-fields';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Columns from '@lucide/svelte/icons/columns-3';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Plus from '@lucide/svelte/icons/plus';
    import Trash from '@lucide/svelte/icons/trash-2';
    import { toast } from 'svelte-sonner';

    const organizationId = $derived(page.params.organizationId || '');

    const customFieldsQuery = getCustomFieldsQuery({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const createField = createCustomFieldMutation({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const deleteField = deleteCustomFieldMutation({
        route: {
            get fieldId() {
                return deleteTarget?.id ?? '';
            },
            get organizationId() {
                return organizationId;
            }
        }
    });

    const updateField = updateCustomFieldMutation({
        route: {
            get fieldId() {
                return editTarget?.id ?? '';
            },
            get organizationId() {
                return organizationId;
            }
        }
    });

    // ── Create dialog ─────────────────────────────────────────────────────────
    let showCreateDialog = $state(false);
    let newFieldName = $state('');
    let newFieldIndexType = $state<IndexType>('keyword');
    let newFieldDescription = $state('');
    let createError = $state('');

    function openCreateDialog() {
        newFieldName = '';
        newFieldIndexType = 'keyword';
        newFieldDescription = '';
        createError = '';
        showCreateDialog = true;
    }

    const isValidFieldName = $derived(/^[a-zA-Z0-9_.-]+$/.test(newFieldName.trim()) && !newFieldName.trim().startsWith('@'));

    async function handleCreate() {
        createError = '';
        try {
            await createField.mutateAsync({
                description: newFieldDescription.trim() || undefined,
                indexType: newFieldIndexType,
                name: newFieldName.trim()
            });
            toast.success(`Custom field "${newFieldName.trim()}" created.`);
            showCreateDialog = false;
        } catch (error: unknown) {
            if (showBillingDialogOnUpgradeProblem(error, organizationId, () => handleCreate())) {
                createError = '';
                return;
            }

            if (error instanceof ProblemDetails) {
                createError = error.errors?.['general']?.[0] ?? error.title ?? error.detail ?? 'An error occurred.';
            } else {
                createError = 'An unexpected error occurred.';
            }
        }
    }

    // ── Edit dialog ───────────────────────────────────────────────────────────
    let editTarget = $state<CustomFieldDefinition | null>(null);
    let showEditDialog = $state(false);
    let editDescription = $state('');
    let editError = $state('');

    function openEditDialog(field: CustomFieldDefinition) {
        editTarget = field;
        editDescription = field.description ?? '';
        editError = '';
        showEditDialog = true;
    }

    async function handleEdit() {
        if (!editTarget) {
            return;
        }

        editError = '';
        try {
            await updateField.mutateAsync({ description: editDescription.trim() });
            toast.success(`Custom field "${editTarget.name}" updated.`);
            showEditDialog = false;
            editTarget = null;
        } catch (error: unknown) {
            if (error instanceof ProblemDetails) {
                editError = error.errors?.['general']?.[0] ?? error.title ?? error.detail ?? 'An error occurred.';
            } else {
                editError = 'An unexpected error occurred.';
            }
        }
    }

    // ── Delete dialog ─────────────────────────────────────────────────────────
    let deleteTarget = $state<CustomFieldDefinition | null>(null);
    let showDeleteDialog = $state(false);
    let deleteError = $state('');
    let deleteConfirmName = $state('');

    function openDeleteDialog(field: CustomFieldDefinition) {
        deleteTarget = field;
        deleteError = '';
        deleteConfirmName = '';
        showDeleteDialog = true;
    }

    const isDeleteConfirmed = $derived(deleteConfirmName.trim() === deleteTarget?.name);

    async function handleDelete() {
        if (!deleteTarget || !isDeleteConfirmed) {
            return;
        }

        const fieldName = deleteTarget.name;
        deleteError = '';
        try {
            await deleteField.mutateAsync();
            toast.success(`Custom field "${fieldName}" deleted. New events will no longer be indexed with this field.`);
            showDeleteDialog = false;
            deleteTarget = null;
        } catch (error: unknown) {
            if (error instanceof ProblemDetails) {
                if (error.status === 409) {
                    deleteError =
                        error.title ?? error.detail ?? `"${fieldName}" is used in one or more saved views. Remove it from all filters before deleting.`;
                } else {
                    deleteError = error.errors?.['general']?.[0] ?? error.title ?? error.detail ?? 'An error occurred.';
                }
            } else {
                deleteError = 'An unexpected error occurred.';
            }
        }
    }

    const allFields = $derived(customFieldsQuery.data ?? []);
    const hasFields = $derived(allFields.length > 0);
</script>

<div class="flex flex-col gap-6">
    <div class="flex items-center justify-between">
        <div>
            <h3 class="text-lg font-medium">Custom Event Fields</h3>
            <Muted>Define indexed fields from event data for filtering and search.</Muted>
        </div>
        <Button onclick={openCreateDialog}>
            <Plus data-icon="inline-start" />
            Add Field
        </Button>
    </div>

    {#if customFieldsQuery.isPending}
        <div class="flex flex-col gap-2">
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-10 w-full" />
            <Skeleton class="h-10 w-4/5" />
        </div>
    {:else if customFieldsQuery.isError}
        <ErrorMessage message="Failed to load custom fields." />
    {:else if !hasFields}
        <div class="flex flex-col items-center justify-center rounded-lg border border-dashed py-14 text-center">
            <Columns class="text-muted-foreground mb-3 size-10 opacity-40" />
            <p class="font-medium">No custom fields yet</p>
            <Muted class="mt-1 max-w-xs">Add a field to start indexing event data properties for use in filters and search.</Muted>
            <Button class="mt-4" onclick={openCreateDialog}>
                <Plus data-icon="inline-start" />
                Add Your First Field
            </Button>
        </div>
    {:else}
        <div class="overflow-hidden rounded-md border">
            <Table.Root>
                <Table.Header>
                    <Table.Row>
                        <Table.Head>Name</Table.Head>
                        <Table.Head>Type</Table.Head>
                        <Table.Head>Description</Table.Head>
                        <Table.Head>Added</Table.Head>
                        <Table.Head class="w-[100px]">Actions</Table.Head>
                    </Table.Row>
                </Table.Header>
                <Table.Body>
                    {#each allFields as field (field.id)}
                        <Table.Row>
                            <Table.Cell class="font-mono text-sm">{field.name}</Table.Cell>
                            <Table.Cell>
                                <Badge variant="secondary" title={INDEX_TYPE_DESCRIPTIONS[field.indexType as IndexType]}>
                                    {INDEX_TYPE_LABELS[field.indexType as IndexType] ?? field.indexType}
                                </Badge>
                            </Table.Cell>
                            <Table.Cell class="text-muted-foreground max-w-xs truncate">{field.description ?? '—'}</Table.Cell>
                            <Table.Cell class="text-muted-foreground text-xs">
                                <DateTime value={field.createdUtc} />
                            </Table.Cell>
                            <Table.Cell>
                                <div class="flex items-center gap-1">
                                    <Button variant="ghost" size="icon" onclick={() => openEditDialog(field)} title="Edit description">
                                        <Pencil data-icon="inline-start" />
                                        <span class="sr-only">Edit</span>
                                    </Button>
                                    <Button variant="ghost" size="icon" onclick={() => openDeleteDialog(field)} title="Delete field">
                                        <Trash data-icon="inline-start" class="text-destructive" />
                                        <span class="sr-only">Delete</span>
                                    </Button>
                                </div>
                            </Table.Cell>
                        </Table.Row>
                    {/each}
                </Table.Body>
            </Table.Root>
        </div>
    {/if}
</div>

<!-- ── Add Custom Field Dialog ───────────────────────────────────────────── -->
<Dialog.Root bind:open={showCreateDialog}>
    <Dialog.Content class="sm:max-w-[480px]">
        <Dialog.Header>
            <Dialog.Title>Add Custom Field</Dialog.Title>
            <Dialog.Description>
                Define a new indexed field from event data. The field name must match a key in your event's extended data.
                <strong>Indexing applies to new events only</strong> — historical events are not backfilled.
            </Dialog.Description>
        </Dialog.Header>

        <form
            class="flex flex-col gap-4"
            onsubmit={(e) => {
                e.preventDefault();
                handleCreate();
            }}
        >
            {#if createError}
                <ErrorMessage message={createError} />
            {/if}

            <Field.Field>
                <Field.Label for="field-name">Field Name <span class="text-destructive">*</span></Field.Label>
                <Input
                    id="field-name"
                    bind:value={newFieldName}
                    placeholder="e.g., customer_id"
                    required
                    maxlength={100}
                    autocomplete="off"
                    spellcheck={false}
                />
                {#if newFieldName && !isValidFieldName}
                    <p class="text-destructive text-xs">Only letters, digits, underscore, dot, and dash. Cannot start with @.</p>
                {:else}
                    <Muted>Must match a key in event data. Letters, digits, <code>_</code> <code>.</code> <code>-</code> only.</Muted>
                {/if}
            </Field.Field>

            <Field.Field>
                <Field.Label for="field-type">Index Type <span class="text-destructive">*</span></Field.Label>
                <Select.Root type="single" value={newFieldIndexType} onValueChange={(v) => (newFieldIndexType = (v ?? 'keyword') as IndexType)}>
                    <Select.Trigger id="field-type" class="w-full">
                        <span class="font-medium">{INDEX_TYPE_LABELS[newFieldIndexType]}</span>
                        <span class="text-muted-foreground ml-2 text-xs">{INDEX_TYPE_DESCRIPTIONS[newFieldIndexType]}</span>
                    </Select.Trigger>
                    <Select.Content class="max-h-72">
                        <Select.Group>
                            {#each INDEX_TYPES as type (type)}
                                <Select.Item value={type} class="flex flex-col items-start gap-0.5 py-2">
                                    <span class="font-medium">{INDEX_TYPE_LABELS[type]}</span>
                                    <span class="text-muted-foreground text-xs">{INDEX_TYPE_DESCRIPTIONS[type]}</span>
                                </Select.Item>
                            {/each}
                        </Select.Group>
                    </Select.Content>
                </Select.Root>
                <Muted>Determines how the field is stored and what filter operators are available.</Muted>
            </Field.Field>

            <Field.Field>
                <Field.Label for="field-desc">Description <span class="text-muted-foreground font-normal">(optional)</span></Field.Label>
                <Textarea id="field-desc" bind:value={newFieldDescription} placeholder="What this field represents..." maxlength={500} rows={2} />
            </Field.Field>

            <Dialog.Footer>
                <Button variant="outline" type="button" onclick={() => (showCreateDialog = false)}>Cancel</Button>
                <Button type="submit" disabled={createField.isPending || !newFieldName.trim() || !isValidFieldName}>
                    {#if createField.isPending}
                        <Spinner data-icon="inline-start" />
                    {/if}
                    Create Field
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>

<!-- ── Edit Field Dialog ──────────────────────────────────────────────────── -->
<Dialog.Root bind:open={showEditDialog}>
    <Dialog.Content class="sm:max-w-[425px]">
        <Dialog.Header>
            <Dialog.Title>Edit Field</Dialog.Title>
            <Dialog.Description>
                Update the description for <code class="bg-muted rounded px-1 py-0.5 text-xs">{editTarget?.name}</code>.
            </Dialog.Description>
        </Dialog.Header>

        <form
            class="flex flex-col gap-4"
            onsubmit={(e) => {
                e.preventDefault();
                handleEdit();
            }}
        >
            {#if editError}
                <ErrorMessage message={editError} />
            {/if}

            <Field.Field>
                <Field.Label for="edit-desc">Description <span class="text-muted-foreground font-normal">(optional)</span></Field.Label>
                <Textarea id="edit-desc" bind:value={editDescription} placeholder="What this field represents..." maxlength={500} rows={3} />
            </Field.Field>

            <Dialog.Footer>
                <Button variant="outline" type="button" onclick={() => (showEditDialog = false)}>Cancel</Button>
                <Button type="submit" disabled={updateField.isPending}>
                    {#if updateField.isPending}
                        <Spinner data-icon="inline-start" />
                    {/if}
                    Save Changes
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>

<!-- ── Delete Confirmation Dialog ────────────────────────────────────────── -->
<Dialog.Root bind:open={showDeleteDialog}>
    <Dialog.Content class="sm:max-w-[480px]">
        <Dialog.Header>
            <Dialog.Title>Delete Custom Field</Dialog.Title>
            <Dialog.Description>
                You are about to stop indexing
                <code class="bg-muted rounded px-1 py-0.5 text-xs">{deleteTarget?.name}</code>.
            </Dialog.Description>
        </Dialog.Header>

        <div class="flex flex-col gap-4">
            <div class="bg-muted/50 flex flex-col gap-2 rounded-md border p-3 text-sm">
                <p class="font-medium">What happens when you delete this field:</p>
                <ul class="text-muted-foreground flex list-none flex-col gap-1">
                    <li>• <strong class="text-foreground">New events stop being indexed</strong> with this field immediately.</li>
                    <li>
                        • <strong class="text-foreground">No data is backfilled</strong> — existing indexed events retain their current data and remain searchable
                        until they age out per your retention policy.
                    </li>
                    <li>
                        • <strong class="text-foreground">Saved view filters</strong> using this field must be removed first — deletion is blocked otherwise.
                    </li>
                    <li>• The index slot stays reserved until retention-aware cleanup is implemented.</li>
                </ul>
            </div>

            {#if deleteError}
                <ErrorMessage message={deleteError} />
            {/if}

            <Field.Field>
                <Field.Label for="delete-confirm">
                    Type <code class="bg-muted rounded px-1 py-0.5 text-xs">{deleteTarget?.name}</code> to confirm
                </Field.Label>
                <Input id="delete-confirm" bind:value={deleteConfirmName} placeholder={deleteTarget?.name} autocomplete="off" spellcheck={false} />
            </Field.Field>
        </div>

        <Dialog.Footer>
            <Button variant="outline" type="button" onclick={() => (showDeleteDialog = false)}>Cancel</Button>
            <Button variant="destructive" onclick={handleDelete} disabled={deleteField.isPending || !isDeleteConfirmed}>
                {#if deleteField.isPending}
                    <Spinner data-icon="inline-start" />
                {/if}
                Delete Field
            </Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
