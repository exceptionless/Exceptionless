<script lang="ts">
    import type { OAuthApplication, OAuthApplicationRequest } from '$features/admin/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Checkbox } from '$comp/ui/checkbox';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
    import { Spinner } from '$comp/ui/spinner';
    import { Textarea } from '$comp/ui/textarea';
    import { postOAuthApplicationMutation, putOAuthApplicationMutation } from '$features/admin/api.svelte';
    import { type OAuthApplicationFormData, OAuthApplicationSchema } from '$features/admin/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import Save from '@lucide/svelte/icons/save';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    interface Props {
        application?: null | OAuthApplication;
    }

    let { application = null }: Props = $props();

    const supportedScopes = [
        {
            description: 'Allows connecting to the MCP endpoint.',
            label: 'MCP',
            value: 'mcp:read'
        },
        {
            description: 'Allows reading project metadata.',
            label: 'Projects',
            value: 'projects:read'
        },
        {
            description: 'Allows reading stack data.',
            label: 'Stacks',
            value: 'stacks:read'
        },
        {
            description: 'Allows changing stack status, snooze, and critical settings.',
            label: 'Stacks Write',
            value: 'stacks:write'
        },
        {
            description: 'Allows reading event details.',
            label: 'Events',
            value: 'events:read'
        },
        {
            description: 'Allows refresh token issuance.',
            label: 'Offline Access',
            value: 'offline_access'
        }
    ] as const;

    const createApplication = postOAuthApplicationMutation();
    const updateApplication = putOAuthApplicationMutation();
    const isEditing = $derived(application !== null);
    const listHref = resolve('/(app)/system/oauth-applications');

    const form = createForm(() => ({
        defaultValues: getFormValues(application),
        validators: {
            onSubmit: OAuthApplicationSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    const request = toRequest(value);
                    if (application) {
                        await updateApplication.mutateAsync({
                            id: application.id,
                            request
                        });
                    } else {
                        await createApplication.mutateAsync(request);
                    }

                    toast.success(isEditing ? 'OAuth application updated.' : 'OAuth application created.');
                    await goto(listHref);
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return {
                        form: 'An unexpected error occurred, please try again.'
                    };
                }
            }
        }
    }));

    function getFormValues(value: null | OAuthApplication): OAuthApplicationFormData {
        return {
            client_id: value?.client_id ?? '',
            is_disabled: value?.is_disabled ?? false,
            name: value?.name ?? '',
            notes: value?.notes ?? '',
            redirect_uris: value?.redirect_uris.join('\n') ?? '',
            scopes: value?.scopes ?? ['mcp:read', 'projects:read', 'stacks:read', 'events:read', 'offline_access']
        };
    }

    function toRequest(value: OAuthApplicationFormData): OAuthApplicationRequest {
        return {
            client_id: value.client_id.trim(),
            is_disabled: value.is_disabled,
            name: value.name.trim(),
            notes: value.notes?.trim() || null,
            redirect_uris: value.redirect_uris
                .split(/\r?\n/)
                .map((item) => item.trim())
                .filter(Boolean),
            scopes: value.scopes
        };
    }

    function isScopeChecked(scopes: string[], scope: string) {
        return scopes.includes(scope);
    }
</script>

<form
    onsubmit={(event) => {
        event.preventDefault();
        form.handleSubmit();
    }}
>
    <Card.Root>
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
                        <Field.Description>One exact redirect URI per line. Wildcards are not supported.</Field.Description>
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                    </Field.Field>
                {/snippet}
            </form.Field>

            <form.Field name="scopes">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label>Scopes</Field.Label>
                        <div class="grid gap-3 sm:grid-cols-2">
                            {#each supportedScopes as scope (scope.value)}
                                {@const checked = isScopeChecked(field.state.value, scope.value)}
                                <div class="flex items-start gap-3">
                                    <Checkbox
                                        {checked}
                                        onCheckedChange={(value) => {
                                            const current = field.state.value;
                                            field.handleChange(value ? [...current, scope.value] : current.filter((item) => item !== scope.value));
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
                            oninput={(event) => field.handleChange(event.currentTarget.value)}
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
                            <p class="text-muted-foreground text-xs">Disabled clients cannot authorize, refresh, or use existing OAuth access tokens.</p>
                        </div>
                    </div>
                {/snippet}
            </form.Field>
        </Card.Content>
        <Card.Footer class="flex justify-end gap-2">
            <Button href={listHref} variant="outline">Cancel</Button>
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
    </Card.Root>
</form>
