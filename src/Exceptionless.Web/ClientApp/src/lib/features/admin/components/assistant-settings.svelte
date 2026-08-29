<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Separator } from '$comp/ui/separator';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import { getAdminAssistantSettingsQuery, putAdminAssistantEnabledSettingsMutation, putAdminAssistantSettingsMutation } from '$features/admin/api.svelte';
    import { type AssistantSettingsFormData, AssistantSettingsSchema } from '$features/admin/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    const settingsQuery = getAdminAssistantSettingsQuery();
    const updateEnabledSettings = putAdminAssistantEnabledSettingsMutation();
    const updateSettings = putAdminAssistantSettingsMutation();
    let assistantEnabled = $state(false);
    let loadedAvailabilityKey = $state<null | string>(null);
    let loadedSettingsKey = $state<null | string>(null);
    const settings = $derived(settingsQuery.data);
    const availabilityKey = $derived(
        settings ? JSON.stringify([settings.enabled, settings.configured_enabled, settings.is_enabled_overridden, settings.is_configured]) : null
    );
    const settingsKey = $derived(settings ? JSON.stringify([settings.model, settings.configured_model, settings.is_overridden]) : null);

    const settingsForm = createForm(() => ({
        defaultValues: {
            model: ''
        } as AssistantSettingsFormData,
        validators: {
            onSubmit: AssistantSettingsSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    const saved = await updateSettings.mutateAsync({
                        model: value.model.trim()
                    });
                    settingsForm.setFieldValue('model', saved.model);
                    toast.success(saved.is_overridden ? 'Exie model override saved.' : 'Exie is using the deployment-configured model.');
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return {
                        form: 'Failed to update the Exie model.'
                    };
                }
            }
        }
    }));

    $effect(() => {
        if (!settings || loadedAvailabilityKey === availabilityKey) {
            return;
        }

        loadedAvailabilityKey = availabilityKey;
        assistantEnabled = settings.enabled;
    });

    $effect(() => {
        if (!settings || loadedSettingsKey === settingsKey) {
            return;
        }

        loadedSettingsKey = settingsKey;
        settingsForm.setFieldValue('model', settings.model);
    });

    async function resetModel() {
        try {
            const saved = await updateSettings.mutateAsync({
                model: null
            });
            settingsForm.setFieldValue('model', saved.model);
            toast.success('Exie model reset to the deployment default.');
        } catch {
            toast.error('Failed to reset the Exie model.');
        }
    }

    async function saveAvailability() {
        try {
            const saved = await updateEnabledSettings.mutateAsync({
                enabled: assistantEnabled
            });
            assistantEnabled = saved.enabled;
            toast.success(saved.enabled ? 'Exie is enabled.' : 'Exie is disabled.');
        } catch {
            toast.error('Failed to update Exie availability.');
        }
    }

    async function resetAvailability() {
        try {
            const saved = await updateEnabledSettings.mutateAsync({
                enabled: null
            });
            assistantEnabled = saved.enabled;
            toast.success('Exie availability reset to the deployment default.');
        } catch {
            toast.error('Failed to reset Exie availability.');
        }
    }
</script>

{#if settingsQuery.isPending}
    <div class="flex items-center gap-2 p-4">
        <Spinner />
        <span class="text-muted-foreground text-sm">Loading Exie settings...</span>
    </div>
{:else if settingsQuery.isError}
    <div class="p-4">
        <p class="text-destructive text-sm">Failed to load Exie settings.</p>
    </div>
{:else}
    <Field.Field orientation="responsive" class="gap-4 p-4">
        <Field.Content>
            <div class="flex flex-wrap items-center gap-2">
                <Field.Label for="assistant-enabled">Exie availability</Field.Label>
                {#if settings}
                    <Badge variant={settings.is_enabled_overridden ? 'secondary' : 'outline'}>
                        {settings.is_enabled_overridden ? 'Runtime override' : 'Deployment default'}
                    </Badge>
                {/if}
            </div>
            <Field.Description>Enable or disable Exie for all organizations. Changes apply without restarting the app.</Field.Description>
            {#if settings && !settings.is_configured}
                <p class="text-destructive text-sm">An OpenRouter API key must be configured before Exie can be used.</p>
            {/if}
        </Field.Content>
        <div class="flex flex-wrap items-center justify-end gap-2">
            <Switch id="assistant-enabled" bind:checked={assistantEnabled} disabled={updateEnabledSettings.isPending} />
            {#if settings?.is_enabled_overridden}
                <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    aria-label="Reset Exie availability to deployment default"
                    disabled={updateEnabledSettings.isPending}
                    onclick={resetAvailability}>Reset</Button
                >
            {/if}
            <Button
                type="button"
                size="sm"
                aria-label="Save Exie availability"
                disabled={updateEnabledSettings.isPending || assistantEnabled === settings?.enabled}
                onclick={saveAvailability}
            >
                {updateEnabledSettings.isPending ? 'Saving...' : 'Save'}
            </Button>
        </div>
    </Field.Field>

    <Separator />

    <form
        onsubmit={(event) => {
            event.preventDefault();
            void settingsForm.handleSubmit();
        }}
    >
        <settingsForm.Field name="model">
            {#snippet children(field)}
                <Field.Field orientation="responsive" class="gap-4 p-4" data-invalid={ariaInvalid(field)}>
                    <Field.Content>
                        <div class="flex flex-wrap items-center gap-2">
                            <Field.Label for={field.name}>Exie model</Field.Label>
                            {#if settings}
                                <Badge variant={settings.is_overridden ? 'secondary' : 'outline'}>
                                    {settings.is_overridden ? 'Runtime override' : 'Deployment default'}
                                </Badge>
                            {/if}
                        </div>
                        <Field.Description>OpenRouter model used for new Exie conversations and turns. Changes apply without restarting.</Field.Description>
                    </Field.Content>
                    <div class="flex w-full flex-col gap-2 @md/field-group:w-[32rem]">
                        <div class="flex flex-col gap-2 sm:flex-row">
                            <Input
                                class="min-w-0 flex-1"
                                id={field.name}
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(event) => field.handleChange(event.currentTarget.value)}
                                aria-invalid={ariaInvalid(field)}
                                autocomplete="off"
                                placeholder="provider/model"
                            />
                            <div class="flex items-center justify-end gap-2">
                                {#if settings?.is_overridden}
                                    <Button
                                        type="button"
                                        size="sm"
                                        variant="outline"
                                        aria-label="Reset Exie model to deployment default"
                                        disabled={updateSettings.isPending}
                                        onclick={resetModel}>Reset</Button
                                    >
                                {/if}
                                <settingsForm.Subscribe selector={(state) => state.isSubmitting}>
                                    {#snippet children(isSubmitting)}
                                        <Button type="submit" size="sm" aria-label="Save Exie model" disabled={isSubmitting || updateSettings.isPending}>
                                            {isSubmitting || updateSettings.isPending ? 'Saving...' : 'Save'}
                                        </Button>
                                    {/snippet}
                                </settingsForm.Subscribe>
                            </div>
                        </div>
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                        <settingsForm.Subscribe selector={(state) => state.errors}>
                            {#snippet children(errors)}
                                <ErrorMessage message={getFormErrorMessages(errors)} />
                            {/snippet}
                        </settingsForm.Subscribe>
                    </div>
                </Field.Field>
            {/snippet}
        </settingsForm.Field>
    </form>
{/if}
