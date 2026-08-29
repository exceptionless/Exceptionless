<script lang="ts">
    import { Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Separator } from '$comp/ui/separator';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import { getEventSubmissionSettingsQuery, putEventSubmissionSettingsMutation } from '$features/admin/api.svelte';
    import AssistantSettings from '$features/admin/components/assistant-settings.svelte';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import { toast } from 'svelte-sonner';

    const settingsQuery = getEventSubmissionSettingsQuery();
    const updateSettings = putEventSubmissionSettingsMutation();
    let eventSubmissionEnabled = $state(false);
    let loadedSettingsKey = $state<null | string>(null);

    const settings = $derived(settingsQuery.data);
    const settingsKey = $derived(settings ? JSON.stringify([settings.enabled, settings.configured_enabled, settings.is_overridden]) : null);

    $effect(() => {
        if (!settings || loadedSettingsKey === settingsKey) {
            return;
        }

        loadedSettingsKey = settingsKey;
        eventSubmissionEnabled = settings.enabled;
    });

    async function saveEventSubmission() {
        try {
            const saved = await updateSettings.mutateAsync({
                enabled: eventSubmissionEnabled
            });
            eventSubmissionEnabled = saved.enabled;
            toast.success(saved.enabled ? 'Event submission is enabled.' : 'Event submission is disabled.');
        } catch {
            toast.error('Failed to update event submission.');
        }
    }

    async function resetEventSubmission() {
        try {
            const saved = await updateSettings.mutateAsync({
                enabled: null
            });
            eventSubmissionEnabled = saved.enabled;
            toast.success('Event submission reset to the deployment default.');
        } catch {
            toast.error('Failed to reset event submission.');
        }
    }
</script>

<div class="flex flex-col gap-4">
    <Muted>Manage runtime system behavior</Muted>

    <Field.FieldGroup class="gap-0 overflow-hidden rounded-lg border">
        <AssistantSettings />
        <Separator />

        <Field.Field orientation="responsive" class="gap-4 p-4">
            <Field.Content>
                <div class="flex flex-wrap items-center gap-2">
                    <Field.Label for="event-submission-enabled">Event submission</Field.Label>
                    {#if settings}
                        <Badge variant={settings.is_overridden ? 'secondary' : 'outline'}>
                            {settings.is_overridden ? 'Runtime override' : 'Deployment default'}
                        </Badge>
                    {/if}
                </div>
                <Field.Description>Control whether Exceptionless accepts new events. Changes apply to new requests immediately.</Field.Description>
                {#if !settingsQuery.isPending && !settingsQuery.isError && !eventSubmissionEnabled}
                    <Alert.Root variant="destructive">
                        <AlertTriangle />
                        <Alert.Title>Incoming events will be rejected with HTTP 503.</Alert.Title>
                        <Alert.Description>Session heartbeats remain successful but will not record activity.</Alert.Description>
                    </Alert.Root>
                {/if}
            </Field.Content>

            {#if settingsQuery.isPending}
                <div class="flex items-center justify-end gap-2">
                    <Spinner />
                    <Muted>Loading...</Muted>
                </div>
            {:else if settingsQuery.isError}
                <p class="text-destructive text-sm">Failed to load event submission settings.</p>
            {:else}
                <div class="flex flex-wrap items-center justify-end gap-2">
                    <Switch id="event-submission-enabled" bind:checked={eventSubmissionEnabled} disabled={updateSettings.isPending} />
                    {#if settings?.is_overridden}
                        <Button type="button" size="sm" variant="outline" disabled={updateSettings.isPending} onclick={resetEventSubmission}>Reset</Button>
                    {/if}
                    <Button
                        type="button"
                        size="sm"
                        disabled={updateSettings.isPending || eventSubmissionEnabled === settings?.enabled}
                        onclick={saveEventSubmission}
                    >
                        {updateSettings.isPending ? 'Saving...' : 'Save'}
                    </Button>
                </div>
            {/if}
        </Field.Field>
    </Field.FieldGroup>
</div>
