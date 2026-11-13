<script lang="ts">
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    import ErrorMessage from '#comp/error-message.svelte';
    import { Muted } from '#comp/typography/index.ts';
    import { Badge } from '#comp/ui/badge/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { Textarea } from '#comp/ui/textarea/index.ts';
    import { getTrialSettingsQuery, putTrialSettingsMutation } from '#features/admin/api.svelte.ts';
    import { type TrialSettingsFormData, TrialSettingsFormSchema } from '#features/admin/schemas.ts';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '#features/shared/validation.ts';

    const settingsQuery = getTrialSettingsQuery();
    const updateSettings = putTrialSettingsMutation();
    let loadedSettingsKey = $state<null | string>(null);

    const settings = $derived(settingsQuery.data);
    const settingsKey = $derived(settings ? JSON.stringify([settings.blocked_email_domains, settings.is_default]) : null);

    const form = createForm(() => ({
        defaultValues: {
            blocked_email_domains: ''
        } as TrialSettingsFormData,
        validators: {
            onSubmit: TrialSettingsFormSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    const saved = await updateSettings.mutateAsync({
                        blocked_email_domains: parseDomains(value.blocked_email_domains)
                    });
                    form.reset({
                        blocked_email_domains: saved.blocked_email_domains.join('\n')
                    });
                    toast.success('Blocked trial domains saved.');
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return {
                        form: 'Failed to update blocked trial domains.'
                    };
                }
            }
        }
    }));

    $effect(() => {
        if (!settings || loadedSettingsKey === settingsKey) {
            return;
        }

        loadedSettingsKey = settingsKey;
        form.reset({
            blocked_email_domains: settings.blocked_email_domains.join('\n')
        });
    });

    function parseDomains(value: string): string[] {
        return [
            ...new Set(
                value
                    .split(/[\s,]+/)
                    .map((domain) => domain.trim().replace(/^@+/, '').toLowerCase())
                    .filter((domain) => domain.length > 0)
            )
        ].sort();
    }

    async function reset() {
        try {
            const saved = await updateSettings.mutateAsync({
                blocked_email_domains: null
            });
            form.reset({
                blocked_email_domains: saved.blocked_email_domains.join('\n')
            });
            toast.success('Blocked trial domains reset to the built-in list.');
        } catch {
            toast.error('Failed to reset blocked trial domains.');
        }
    }
</script>

{#if settingsQuery.isPending}
    <div class="flex items-center gap-2 p-4">
        <Spinner />
        <Muted>Loading free trial settings...</Muted>
    </div>
{:else if settingsQuery.isError}
    <div class="p-4">
        <ErrorMessage message="Failed to load free trial settings." />
    </div>
{:else if settings}
    <form.Field name="blocked_email_domains">
        {#snippet children(field)}
            {@const enteredDomains = parseDomains(field.state.value)}
            {@const hasChanges = enteredDomains.join('\n') !== settings.blocked_email_domains.join('\n')}
            <Field.Field orientation="vertical" class="gap-4 p-4" data-invalid={ariaInvalid(field)}>
                <Field.Content>
                    <div class="flex flex-wrap items-center gap-2">
                        <Field.Label for="trial-blocked-email-domains">Blocked trial email domains</Field.Label>
                        <Badge variant={settings.is_default ? 'outline' : 'secondary'}>{settings.is_default ? 'Built-in list' : 'Custom list'}</Badge>
                    </div>
                    <Field.Description>
                        Signups from these domains and their subdomains get the Free plan instead of a free trial. Enter one domain per line.
                    </Field.Description>
                </Field.Content>
                <Textarea
                    id="trial-blocked-email-domains"
                    value={field.state.value}
                    onblur={field.handleBlur}
                    oninput={(event) => field.handleChange(event.currentTarget.value)}
                    rows={8}
                    class="font-mono text-sm"
                    aria-invalid={ariaInvalid(field)}
                    aria-describedby="trial-blocked-email-domains-error"
                    disabled={updateSettings.isPending}
                />
                <Field.Error id="trial-blocked-email-domains-error" errors={mapFieldErrors(field.state.meta.errors)} />
                <form.Subscribe selector={(state) => state.errors}>
                    {#snippet children(errors)}
                        <ErrorMessage message={getFormErrorMessages(errors)} />
                    {/snippet}
                </form.Subscribe>
                <div class="flex flex-wrap items-center justify-between gap-2">
                    <Muted>{enteredDomains.length === 1 ? '1 domain' : `${enteredDomains.length} domains`}</Muted>
                    <form.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <div class="flex flex-wrap items-center gap-2">
                                {#if !settings.is_default}
                                    <Button
                                        type="button"
                                        size="sm"
                                        variant="outline"
                                        aria-label="Reset blocked trial domains to the built-in list"
                                        disabled={isSubmitting || updateSettings.isPending}
                                        onclick={reset}>Reset</Button
                                    >
                                {/if}
                                <Button
                                    type="button"
                                    size="sm"
                                    aria-label="Save blocked trial domains"
                                    disabled={isSubmitting || updateSettings.isPending || !hasChanges}
                                    onclick={() => form.handleSubmit()}
                                >
                                    {updateSettings.isPending ? 'Saving...' : 'Save'}
                                </Button>
                            </div>
                        {/snippet}
                    </form.Subscribe>
                </div>
            </Field.Field>
        {/snippet}
    </form.Field>
{/if}
