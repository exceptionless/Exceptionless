<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import { getAdminAssistantSettingsQuery, putAdminAssistantEnabledSettingsMutation, putAdminAssistantSettingsMutation } from '$features/admin/api.svelte';
    import { type AssistantSettingsFormData, AssistantSettingsSchema } from '$features/admin/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import RotateCcw from '@lucide/svelte/icons/rotate-ccw';
    import Save from '@lucide/svelte/icons/save';
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

<section class="space-y-4" aria-labelledby="exie-settings-heading">
    <div class="space-y-1">
        <h2 id="exie-settings-heading" class="text-lg font-semibold">Exie</h2>
        <Muted>Manage Exie availability and provider configuration.</Muted>
    </div>

    <Card.Root>
        <Card.Header>
            <div class="flex flex-wrap items-center gap-2">
                <Card.Title>Availability</Card.Title>
                {#if settings}
                    <Badge variant={settings.is_enabled_overridden ? 'secondary' : 'outline'}>
                        {settings.is_enabled_overridden ? 'Runtime override' : 'Deployment default'}
                    </Badge>
                {/if}
            </div>
            <Card.Description>Enable or disable Exie for all organizations without restarting the app.</Card.Description>
        </Card.Header>
        {#if settingsQuery.isPending}
            <Card.Content class="flex items-center gap-2">
                <Spinner />
                <Muted>Loading Exie availability...</Muted>
            </Card.Content>
        {:else if settingsQuery.isError}
            <Card.Content>
                <p class="text-destructive text-sm">Failed to load Exie availability.</p>
            </Card.Content>
        {:else}
            <Card.Content class="space-y-2">
                <div class="flex items-center justify-between gap-6 rounded-lg border p-4">
                    <div class="space-y-1">
                        <label class="text-sm font-medium" for="assistant-enabled">Exie enabled</label>
                        <Muted class="text-xs"
                            >Disabled Exie requests are rejected immediately. The deployment default is {settings?.configured_enabled
                                ? 'enabled'
                                : 'disabled'}.</Muted
                        >
                    </div>
                    <Switch id="assistant-enabled" bind:checked={assistantEnabled} disabled={updateEnabledSettings.isPending} />
                </div>
                {#if settings && !settings.is_configured}
                    <p class="text-destructive text-sm">An OpenRouter API key must be configured before Exie can be used.</p>
                {/if}
            </Card.Content>
            <Card.Footer class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                {#if settings?.is_enabled_overridden}
                    <Button type="button" variant="outline" disabled={updateEnabledSettings.isPending} onclick={resetAvailability}>
                        <RotateCcw data-icon="inline-start" />
                        Reset to Deployment Default
                    </Button>
                {/if}
                <Button type="button" disabled={updateEnabledSettings.isPending || assistantEnabled === settings?.enabled} onclick={saveAvailability}>
                    {#if updateEnabledSettings.isPending}
                        <Spinner data-icon="inline-start" />
                        Saving...
                    {:else}
                        <Save data-icon="inline-start" />
                        Save Availability
                    {/if}
                </Button>
            </Card.Footer>
        {/if}
    </Card.Root>

    <Card.Root>
        <Card.Header>
            <div class="flex flex-wrap items-center gap-2">
                <Card.Title>Model Configuration</Card.Title>
                {#if settings}
                    <Badge variant={settings.is_overridden ? 'secondary' : 'outline'}>
                        {settings.is_overridden ? 'Runtime override' : 'Deployment default'}
                    </Badge>
                {/if}
            </div>
            <Card.Description>Choose the OpenRouter model used for new Exie conversations and turns. Changes apply without restarting the app.</Card.Description
            >
        </Card.Header>
        {#if settingsQuery.isPending}
            <Card.Content class="flex items-center gap-2">
                <Spinner />
                <Muted>Loading model configuration...</Muted>
            </Card.Content>
        {:else if settingsQuery.isError}
            <Card.Content>
                <p class="text-destructive text-sm">Failed to load the Exie model configuration.</p>
            </Card.Content>
        {:else}
            <form
                onsubmit={(event) => {
                    event.preventDefault();
                    void settingsForm.handleSubmit();
                }}
            >
                <Card.Content>
                    <Field.FieldGroup>
                        <settingsForm.Field name="model">
                            {#snippet children(field)}
                                <Field.Field data-invalid={ariaInvalid(field)}>
                                    <Field.Label for={field.name}>OpenRouter model ID</Field.Label>
                                    <Input
                                        id={field.name}
                                        value={field.state.value}
                                        onblur={field.handleBlur}
                                        oninput={(event) => field.handleChange(event.currentTarget.value)}
                                        aria-invalid={ariaInvalid(field)}
                                        autocomplete="off"
                                        placeholder="z-ai/glm-5.3-flash"
                                    />
                                    <Field.Description>
                                        Enter a model slug such as
                                        <a href="https://openrouter.ai/z-ai/glm-5.3-flash" target="_blank" rel="noopener noreferrer">z-ai/glm-5.3-flash</a>. The
                                        deployment default is <code>{settings?.configured_model}</code>.
                                    </Field.Description>
                                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                </Field.Field>
                            {/snippet}
                        </settingsForm.Field>
                        <settingsForm.Subscribe selector={(state) => state.errors}>
                            {#snippet children(errors)}
                                <ErrorMessage message={getFormErrorMessages(errors)} />
                            {/snippet}
                        </settingsForm.Subscribe>
                    </Field.FieldGroup>
                </Card.Content>
                <Card.Footer class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                    {#if settings?.is_overridden}
                        <Button type="button" variant="outline" disabled={updateSettings.isPending} onclick={resetModel}>
                            <RotateCcw data-icon="inline-start" />
                            Reset to Deployment Default
                        </Button>
                    {/if}
                    <settingsForm.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <Button type="submit" disabled={isSubmitting || updateSettings.isPending}>
                                {#if isSubmitting || updateSettings.isPending}
                                    <Spinner data-icon="inline-start" />
                                    Saving...
                                {:else}
                                    <Save data-icon="inline-start" />
                                    Save Model
                                {/if}
                            </Button>
                        {/snippet}
                    </settingsForm.Subscribe>
                </Card.Footer>
            </form>
        {/if}
    </Card.Root>
</section>
