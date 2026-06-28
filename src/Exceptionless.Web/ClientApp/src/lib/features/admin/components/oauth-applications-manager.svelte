<script lang="ts">
    import type { OAuthApplication, OAuthApplicationRequest } from '$features/admin/models';

    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import ErrorMessage from '$comp/error-message.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { Muted } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Checkbox } from '$comp/ui/checkbox';
    import * as Dialog from '$comp/ui/dialog';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import * as Table from '$comp/ui/table';
    import { Textarea } from '$comp/ui/textarea';
    import {
        deleteOAuthApplicationMutation,
        getOAuthApplicationsQuery,
        postOAuthApplicationMutation,
        putOAuthApplicationMutation
    } from '$features/admin/api.svelte';
    import { type OAuthApplicationFormData, OAuthApplicationSchema } from '$features/admin/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Check from '@lucide/svelte/icons/check';
    import CircleSlash from '@lucide/svelte/icons/circle-slash';
    import Ellipsis from '@lucide/svelte/icons/ellipsis';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Plus from '@lucide/svelte/icons/plus';
    import Save from '@lucide/svelte/icons/save';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    type Props = {
        description?: string;
        note?: string;
    };

    let { description = 'Manage OAuth clients that can request access to the Exceptionless API and MCP tools.', note }: Props = $props();

    const supportedScopes = [
        { description: 'Allows connecting to the MCP endpoint.', label: 'MCP', value: 'mcp:read' },
        { description: 'Allows reading project metadata.', label: 'Projects', value: 'projects:read' },
        { description: 'Allows reading stack data.', label: 'Stacks', value: 'stacks:read' },
        { description: 'Allows changing stack status, snooze, and critical settings.', label: 'Stacks Write', value: 'stacks:write' },
        { description: 'Allows reading event details.', label: 'Events', value: 'events:read' },
        { description: 'Allows refresh token issuance.', label: 'Offline Access', value: 'offline_access' }
    ] as const;

    const applicationsQuery = getOAuthApplicationsQuery();
    const createApplication = postOAuthApplicationMutation();
    const updateApplication = putOAuthApplicationMutation();
    const deleteApplication = deleteOAuthApplicationMutation();

    let deleteDialogOpen = $state(false);
    let editorOpen = $state(false);
    let pendingDeleteApplication = $state<null | OAuthApplication>(null);
    let pendingStatusApplication = $state<null | OAuthApplication>(null);
    let selectedApplication = $state<null | OAuthApplication>(null);
    let statusDialogOpen = $state(false);
    let toastId = $state<number | string>();

    const applications = $derived(applicationsQuery.data ?? []);
    const isEditing = $derived(selectedApplication !== null);
    const statusAction = $derived(pendingStatusApplication?.is_disabled ? 'enable' : 'disable');

    const form = createForm(() => ({
        defaultValues: getFormValues(null),
        validators: {
            onSubmit: OAuthApplicationSchema,
            onSubmitAsync: async ({ value }) => {
                const wasEditing = selectedApplication !== null;
                const request = toRequest(value, selectedApplication?.is_disabled ?? false);

                try {
                    const saved = selectedApplication
                        ? await updateApplication.mutateAsync({ id: selectedApplication.id, request })
                        : await createApplication.mutateAsync(request);

                    selectedApplication = saved;
                    setFormValues(saved);
                    editorOpen = false;
                    toast.dismiss(toastId);
                    toastId = toast.success(wasEditing ? 'OAuth application updated.' : 'OAuth application created.');
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred, please try again.' };
                }
            }
        }
    }));

    $effect(() => {
        if (!deleteDialogOpen) {
            pendingDeleteApplication = null;
        }
    });

    $effect(() => {
        if (!statusDialogOpen) {
            pendingStatusApplication = null;
        }
    });

    function getFormValues(application: null | OAuthApplication): OAuthApplicationFormData {
        return {
            client_id: application?.client_id ?? '',
            is_disabled: application?.is_disabled ?? false,
            name: application?.name ?? '',
            notes: application?.notes ?? '',
            redirect_uris: application?.redirect_uris.join('\n') ?? '',
            scopes: application?.scopes ?? ['mcp:read', 'projects:read', 'stacks:read', 'events:read', 'offline_access']
        };
    }

    function setFormValues(application: null | OAuthApplication) {
        const values = getFormValues(application);
        form.setFieldValue('client_id', values.client_id);
        form.setFieldValue('is_disabled', values.is_disabled);
        form.setFieldValue('name', values.name);
        form.setFieldValue('notes', values.notes);
        form.setFieldValue('redirect_uris', values.redirect_uris);
        form.setFieldValue('scopes', values.scopes);
    }

    function toRequest(value: OAuthApplicationFormData, isDisabled: boolean): OAuthApplicationRequest {
        return {
            client_id: value.client_id.trim(),
            is_disabled: isDisabled,
            name: value.name.trim(),
            notes: value.notes?.trim() || null,
            redirect_uris: value.redirect_uris
                .split(/\r?\n/)
                .map((v) => v.trim())
                .filter(Boolean),
            scopes: value.scopes
        };
    }

    function toRequestFromApplication(application: OAuthApplication, isDisabled: boolean): OAuthApplicationRequest {
        return {
            client_id: application.client_id,
            is_disabled: isDisabled,
            name: application.name,
            notes: application.notes ?? null,
            redirect_uris: application.redirect_uris,
            scopes: application.scopes
        };
    }

    function openCreateDialog() {
        selectedApplication = null;
        form.reset();
        setFormValues(null);
        editorOpen = true;
    }

    function openEditDialog(application: OAuthApplication) {
        selectedApplication = application;
        form.reset();
        setFormValues(application);
        editorOpen = true;
    }

    function openDeleteDialog(application: OAuthApplication) {
        pendingDeleteApplication = application;
        deleteDialogOpen = true;
    }

    function openStatusDialog(application: OAuthApplication) {
        pendingStatusApplication = application;
        statusDialogOpen = true;
    }

    async function handleDelete() {
        const application = pendingDeleteApplication;
        if (!application) {
            return;
        }

        try {
            await deleteApplication.mutateAsync(application.id);
            if (selectedApplication?.id === application.id) {
                selectedApplication = null;
                form.reset();
                setFormValues(null);
                editorOpen = false;
            }

            deleteDialogOpen = false;
            toast.success('OAuth application deleted.');
        } catch {
            toast.error('Failed to delete OAuth application.');
        }
    }

    async function handleStatusChange() {
        const application = pendingStatusApplication;
        if (!application) {
            return;
        }

        const isDisabled = !application.is_disabled;

        try {
            const saved = await updateApplication.mutateAsync({
                id: application.id,
                request: toRequestFromApplication(application, isDisabled)
            });

            if (selectedApplication?.id === application.id) {
                selectedApplication = saved;
                setFormValues(saved);
            }

            statusDialogOpen = false;
            toast.success(`OAuth application ${isDisabled ? 'disabled' : 'enabled'}.`);
        } catch {
            toast.error(`Failed to ${isDisabled ? 'disable' : 'enable'} OAuth application.`);
        }
    }

    function isScopeChecked(scopes: string[], scope: string) {
        return scopes.includes(scope);
    }
</script>

<div class="flex flex-col gap-6">
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div class="flex flex-col gap-1">
            <Muted>{description}</Muted>
            {#if note}
                <p class="text-muted-foreground text-xs">{note}</p>
            {/if}
        </div>
        <Button variant="outline" onclick={openCreateDialog}>
            <Plus data-icon="inline-start" />
            New OAuth App
        </Button>
    </div>

    <Card.Root>
        <Card.Header>
            <Card.Title class="text-sm font-medium">OAuth Applications</Card.Title>
            <Card.Description>Registered public OAuth clients.</Card.Description>
        </Card.Header>
        <Card.Content>
            {#if applicationsQuery.isPending}
                <div class="text-muted-foreground flex items-center gap-2 py-8 text-sm">
                    <Spinner />
                    Loading OAuth applications...
                </div>
            {:else if applicationsQuery.isError}
                <p class="text-destructive py-8 text-sm">Failed to load OAuth applications.</p>
            {:else if applications.length === 0}
                <p class="text-muted-foreground py-8 text-sm">No OAuth applications have been created.</p>
            {:else}
                <div class="overflow-x-auto">
                    <Table.Root>
                        <Table.Header>
                            <Table.Row>
                                <Table.Head>Name</Table.Head>
                                <Table.Head>Client ID</Table.Head>
                                <Table.Head>Scopes</Table.Head>
                                <Table.Head>Status</Table.Head>
                                <Table.Head class="w-12 text-right">
                                    <span class="sr-only">Actions</span>
                                </Table.Head>
                            </Table.Row>
                        </Table.Header>
                        <Table.Body>
                            {#each applications as application (application.id)}
                                <Table.Row>
                                    <Table.Cell>
                                        <div class="font-medium">{application.name}</div>
                                        <div class="text-muted-foreground mt-1 text-xs">
                                            Updated <DateTime value={application.updated_utc} />
                                        </div>
                                    </Table.Cell>
                                    <Table.Cell>
                                        <div class="flex items-center gap-2">
                                            <code class="bg-muted rounded px-1.5 py-0.5 text-xs break-all">{application.client_id}</code>
                                            <CopyToClipboardButton value={application.client_id} variant="ghost" size="icon" />
                                        </div>
                                    </Table.Cell>
                                    <Table.Cell>
                                        <div class="flex max-w-64 flex-wrap gap-1">
                                            {#each application.scopes as scope (scope)}
                                                <Badge variant="secondary">{scope}</Badge>
                                            {/each}
                                        </div>
                                    </Table.Cell>
                                    <Table.Cell>
                                        {#if application.is_disabled}
                                            <Badge variant="outline" class="gap-1">
                                                <CircleSlash aria-hidden="true" />
                                                Disabled
                                            </Badge>
                                        {:else}
                                            <Badge variant="secondary" class="gap-1">
                                                <Check aria-hidden="true" />
                                                Enabled
                                            </Badge>
                                        {/if}
                                    </Table.Cell>
                                    <Table.Cell class="text-right">
                                        <DropdownMenu.Root>
                                            <DropdownMenu.Trigger>
                                                {#snippet child({ props })}
                                                    <Button {...props} variant="ghost" size="icon" aria-label={`Open actions for ${application.name}`}>
                                                        <Ellipsis data-icon="inline-start" />
                                                    </Button>
                                                {/snippet}
                                            </DropdownMenu.Trigger>
                                            <DropdownMenu.Content align="end">
                                                <DropdownMenu.Group>
                                                    <DropdownMenu.Item onclick={() => openEditDialog(application)}>
                                                        <Pencil />
                                                        Edit
                                                    </DropdownMenu.Item>
                                                    <DropdownMenu.Item onclick={() => openStatusDialog(application)} disabled={updateApplication.isPending}>
                                                        {#if application.is_disabled}
                                                            <Check />
                                                            Enable
                                                        {:else}
                                                            <CircleSlash />
                                                            Disable
                                                        {/if}
                                                    </DropdownMenu.Item>
                                                </DropdownMenu.Group>
                                                <DropdownMenu.Separator />
                                                <DropdownMenu.Group>
                                                    <DropdownMenu.Item
                                                        variant="destructive"
                                                        onclick={() => openDeleteDialog(application)}
                                                        disabled={deleteApplication.isPending}
                                                    >
                                                        <Trash2 />
                                                        Delete
                                                    </DropdownMenu.Item>
                                                </DropdownMenu.Group>
                                            </DropdownMenu.Content>
                                        </DropdownMenu.Root>
                                    </Table.Cell>
                                </Table.Row>
                            {/each}
                        </Table.Body>
                    </Table.Root>
                </div>
            {/if}
        </Card.Content>
    </Card.Root>
</div>

<Dialog.Root bind:open={editorOpen}>
    <Dialog.Content class="sm:max-w-2xl">
        <form
            onsubmit={(event) => {
                event.preventDefault();
                form.handleSubmit();
            }}
        >
            <Dialog.Header>
                <Dialog.Title>{isEditing ? 'Edit OAuth App' : 'New OAuth App'}</Dialog.Title>
                <Dialog.Description>Use exact redirect URIs. Wildcards are not supported.</Dialog.Description>
            </Dialog.Header>

            <div class="flex flex-col gap-5 py-2">
                <form.Subscribe selector={(state) => state.errors}>
                    {#snippet children(errors)}
                        <ErrorMessage message={getFormErrorMessages(errors)} />
                    {/snippet}
                </form.Subscribe>

                <Field.FieldGroup>
                    <form.Field name="name">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>Name</Field.Label>
                                <Input
                                    id={field.name}
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(event) => field.handleChange(event.currentTarget.value)}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <form.Field name="client_id">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>Client ID</Field.Label>
                                <Input
                                    id={field.name}
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(event) => field.handleChange(event.currentTarget.value)}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <form.Field name="redirect_uris">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>Redirect URIs</Field.Label>
                                <Textarea
                                    id={field.name}
                                    class="min-h-24 font-mono text-xs"
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(event) => field.handleChange(event.currentTarget.value)}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Description>One redirect URI per line.</Field.Description>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <form.Field name="scopes">
                        {#snippet children(field)}
                            <Field.FieldSet data-invalid={ariaInvalid(field)}>
                                <Field.FieldLegend variant="label">Scopes</Field.FieldLegend>
                                <Field.Description>Choose the API access this OAuth application can request.</Field.Description>
                                <Field.FieldGroup class="gap-3">
                                    {#each supportedScopes as scope (scope.value)}
                                        {@const checkboxId = `oauth-scope-${scope.value.replace(':', '-')}`}
                                        {@const checked = isScopeChecked(field.state.value, scope.value)}
                                        <Field.Field orientation="horizontal">
                                            <Checkbox
                                                id={checkboxId}
                                                {checked}
                                                onCheckedChange={(value) => {
                                                    const current = field.state.value;
                                                    field.handleChange(value ? [...current, scope.value] : current.filter((s) => s !== scope.value));
                                                }}
                                                aria-invalid={ariaInvalid(field)}
                                            />
                                            <Field.Content>
                                                <Field.Label for={checkboxId} class="font-normal">{scope.label}</Field.Label>
                                                <Field.Description>{scope.description}</Field.Description>
                                            </Field.Content>
                                        </Field.Field>
                                    {/each}
                                </Field.FieldGroup>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.FieldSet>
                        {/snippet}
                    </form.Field>

                    <form.Field name="notes">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>Notes</Field.Label>
                                <Textarea
                                    id={field.name}
                                    value={field.state.value ?? ''}
                                    onblur={field.handleBlur}
                                    oninput={(event) => field.handleChange(event.currentTarget.value)}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                </Field.FieldGroup>
            </div>

            <Dialog.Footer>
                <Button type="button" variant="outline" onclick={() => (editorOpen = false)}>Cancel</Button>
                <form.Subscribe selector={(state) => state.isSubmitting}>
                    {#snippet children(isSubmitting)}
                        <Button type="submit" disabled={isSubmitting}>
                            {#if isSubmitting}
                                <Spinner data-icon="inline-start" />
                                Saving...
                            {:else}
                                <Save data-icon="inline-start" />
                                {isEditing ? 'Save Changes' : 'Create App'}
                            {/if}
                        </Button>
                    {/snippet}
                </form.Subscribe>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>

<AlertDialog.Root bind:open={statusDialogOpen}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>{statusAction === 'enable' ? 'Enable OAuth App' : 'Disable OAuth App'}</AlertDialog.Title>
            <AlertDialog.Description>
                {#if statusAction === 'enable'}
                    Enable "{pendingStatusApplication?.name}" so it can authorize and use OAuth tokens again?
                {:else}
                    Disable "{pendingStatusApplication?.name}"? Existing OAuth access and refresh tokens for this client will stop working.
                {/if}
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action
                variant={statusAction === 'enable' ? 'default' : 'destructive'}
                onclick={() => {
                    void handleStatusChange();
                }}
                disabled={updateApplication.isPending}
            >
                {statusAction === 'enable' ? 'Enable OAuth App' : 'Disable OAuth App'}
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>

<AlertDialog.Root bind:open={deleteDialogOpen}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Delete OAuth App</AlertDialog.Title>
            <AlertDialog.Description>
                Delete "{pendingDeleteApplication?.name}"? This removes the client registration. Disable the client instead when you only need to block access.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action
                variant="destructive"
                onclick={() => {
                    void handleDelete();
                }}
                disabled={deleteApplication.isPending}
            >
                Delete OAuth App
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
