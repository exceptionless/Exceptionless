<script lang="ts">
    import type { OAuthApplication, OAuthApplicationRequest } from '$features/admin/models';

    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import ErrorMessage from '$comp/error-message.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Checkbox } from '$comp/ui/checkbox';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
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
    import Plus from '@lucide/svelte/icons/plus';
    import Save from '@lucide/svelte/icons/save';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    const supportedScopes = [
        { description: 'Allows connecting to the MCP endpoint.', label: 'MCP', value: 'mcp:read' },
        { description: 'Allows reading project metadata.', label: 'Projects', value: 'projects:read' },
        { description: 'Allows reading stack data.', label: 'Stacks', value: 'stacks:read' },
        { description: 'Allows reading event details.', label: 'Events', value: 'events:read' },
        { description: 'Allows refresh token issuance.', label: 'Offline Access', value: 'offline_access' }
    ] as const;

    const applicationsQuery = getOAuthApplicationsQuery();
    const createApplication = postOAuthApplicationMutation();
    const updateApplication = putOAuthApplicationMutation();
    const deleteApplication = deleteOAuthApplicationMutation();

    let selectedApplication = $state<null | OAuthApplication>(null);
    let toastId = $state<number | string>();

    const applications = $derived(applicationsQuery.data ?? []);
    const isEditing = $derived(selectedApplication !== null);

    const form = createForm(() => ({
        defaultValues: getFormValues(null),
        validators: {
            onSubmit: OAuthApplicationSchema,
            onSubmitAsync: async ({ value }) => {
                const request = toRequest(value);

                try {
                    const saved = selectedApplication
                        ? await updateApplication.mutateAsync({ id: selectedApplication.id, request })
                        : await createApplication.mutateAsync(request);

                    selectedApplication = saved;
                    setFormValues(saved);
                    toast.dismiss(toastId);
                    toastId = toast.success(selectedApplication ? 'OAuth application updated.' : 'OAuth application created.');
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

    function toRequest(value: OAuthApplicationFormData): OAuthApplicationRequest {
        return {
            client_id: value.client_id.trim(),
            is_disabled: value.is_disabled,
            name: value.name.trim(),
            notes: value.notes?.trim() || null,
            redirect_uris: value.redirect_uris
                .split(/\r?\n/)
                .map((v) => v.trim())
                .filter(Boolean),
            scopes: value.scopes
        };
    }

    function selectApplication(application: OAuthApplication) {
        selectedApplication = application;
        setFormValues(application);
    }

    function createNewApplication() {
        selectedApplication = null;
        form.reset();
        setFormValues(null);
    }

    async function handleDelete(application: OAuthApplication) {
        if (!confirm(`Delete OAuth application "${application.name}"? Disable the client instead when you need to block OAuth access.`)) {
            return;
        }

        try {
            await deleteApplication.mutateAsync(application.id);
            if (selectedApplication?.id === application.id) {
                createNewApplication();
            }

            toast.success('OAuth application deleted.');
        } catch {
            toast.error('Failed to delete OAuth application.');
        }
    }

    function isScopeChecked(scopes: string[], scope: string) {
        return scopes.includes(scope);
    }
</script>

<div class="space-y-6">
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Muted>Manage OAuth clients that can request access to the Exceptionless API and MCP tools.</Muted>
        <Button variant="outline" onclick={createNewApplication}>
            <Plus class="size-4" aria-hidden="true" />
            New OAuth App
        </Button>
    </div>

    <div class="grid gap-6 xl:grid-cols-[minmax(0,1fr)_420px]">
        <Card.Root>
            <Card.Header>
                <Card.Title class="text-sm font-medium">Applications</Card.Title>
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
                                    <Table.Head class="w-16 text-right">Actions</Table.Head>
                                </Table.Row>
                            </Table.Header>
                            <Table.Body>
                                {#each applications as application (application.id)}
                                    <Table.Row class={selectedApplication?.id === application.id ? 'bg-muted/60' : undefined}>
                                        <Table.Cell>
                                            <button type="button" class="text-left font-medium hover:underline" onclick={() => selectApplication(application)}>
                                                {application.name}
                                            </button>
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
                                                    <CircleSlash class="size-3" aria-hidden="true" />
                                                    Disabled
                                                </Badge>
                                            {:else}
                                                <Badge variant="secondary" class="gap-1">
                                                    <Check class="size-3" aria-hidden="true" />
                                                    Enabled
                                                </Badge>
                                            {/if}
                                        </Table.Cell>
                                        <Table.Cell class="text-right">
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                aria-label="Delete OAuth application"
                                                title="Delete OAuth application"
                                                disabled={deleteApplication.isPending}
                                                onclick={() => {
                                                    void handleDelete(application);
                                                }}
                                            >
                                                <Trash2 class="size-4" aria-hidden="true" />
                                            </Button>
                                        </Table.Cell>
                                    </Table.Row>
                                {/each}
                            </Table.Body>
                        </Table.Root>
                    </div>
                {/if}
            </Card.Content>
        </Card.Root>

        <Card.Root>
            <Card.Header>
                <Card.Title class="text-sm font-medium">{isEditing ? 'Edit OAuth App' : 'New OAuth App'}</Card.Title>
                <Card.Description>Use exact redirect URIs. Wildcards are not supported.</Card.Description>
            </Card.Header>
            <form
                onsubmit={(event) => {
                    event.preventDefault();
                    form.handleSubmit();
                }}
            >
                <Card.Content class="space-y-5">
                    <form.Subscribe selector={(state) => state.errors}>
                        {#snippet children(errors)}
                            <ErrorMessage message={getFormErrorMessages(errors)} />
                        {/snippet}
                    </form.Subscribe>

                    <form.Field name="name">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>Name</Field.Label>
                                <Input
                                    id={field.name}
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(e) => field.handleChange(e.currentTarget.value)}
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
                                    oninput={(e) => field.handleChange(e.currentTarget.value)}
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
                                    oninput={(e) => field.handleChange(e.currentTarget.value)}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Description>One redirect URI per line.</Field.Description>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <form.Field name="scopes">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label>Scopes</Field.Label>
                                <div class="space-y-3">
                                    {#each supportedScopes as scope (scope.value)}
                                        {@const checked = isScopeChecked(field.state.value, scope.value)}
                                        <div class="flex items-start gap-3">
                                            <Checkbox
                                                {checked}
                                                onCheckedChange={(value) => {
                                                    const current = field.state.value;
                                                    field.handleChange(value ? [...current, scope.value] : current.filter((s) => s !== scope.value));
                                                }}
                                            />
                                            <div class="space-y-0.5">
                                                <Label class="text-sm">{scope.label}</Label>
                                                <p class="text-muted-foreground text-xs">{scope.description}</p>
                                            </div>
                                        </div>
                                    {/each}
                                </div>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
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
                                    oninput={(e) => field.handleChange(e.currentTarget.value)}
                                    aria-invalid={ariaInvalid(field)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <form.Field name="is_disabled">
                        {#snippet children(field)}
                            <div class="flex items-start gap-3 rounded-md border p-3">
                                <Checkbox checked={field.state.value} onCheckedChange={(value) => field.handleChange(!!value)} />
                                <div class="space-y-1">
                                    <Label>Disabled</Label>
                                    <p class="text-muted-foreground text-xs">
                                        Disabled clients cannot authorize, refresh, or use existing OAuth access tokens.
                                    </p>
                                </div>
                            </div>
                        {/snippet}
                    </form.Field>
                </Card.Content>
                <Card.Footer class="flex justify-end gap-2">
                    {#if isEditing}
                        <Button type="button" variant="outline" onclick={createNewApplication}>Cancel</Button>
                    {/if}
                    <form.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <Button type="submit" disabled={isSubmitting}>
                                {#if isSubmitting}
                                    <Spinner />
                                    Saving...
                                {:else}
                                    <Save class="size-4" aria-hidden="true" />
                                    {isEditing ? 'Save Changes' : 'Create App'}
                                {/if}
                            </Button>
                        {/snippet}
                    </form.Subscribe>
                </Card.Footer>
            </form>
        </Card.Root>
    </div>
</div>
