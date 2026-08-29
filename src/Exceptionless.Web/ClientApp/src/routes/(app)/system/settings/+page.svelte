<script lang="ts">
    import { Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import { getEventSubmissionSettingsQuery, putEventSubmissionSettingsMutation } from '$features/admin/api.svelte';
    import AssistantSettings from '$features/admin/components/assistant-settings.svelte';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import RotateCcw from '@lucide/svelte/icons/rotate-ccw';
    import Save from '@lucide/svelte/icons/save';
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

<div class="space-y-8">
    <Muted>Manage runtime system behavior</Muted>

    <AssistantSettings />

    <Card.Root>
        <Card.Header>
            <div class="flex flex-wrap items-center gap-2">
                <Card.Title>Event Submission</Card.Title>
                {#if settings}
                    <Badge variant={settings.is_overridden ? 'secondary' : 'outline'}>
                        {settings.is_overridden ? 'Runtime override' : 'Deployment default'}
                    </Badge>
                {/if}
            </div>
            <Card.Description>Control whether Exceptionless accepts new events without restarting the app.</Card.Description>
        </Card.Header>
        {#if settingsQuery.isPending}
            <Card.Content class="flex items-center gap-2">
                <Spinner />
                <Muted>Loading event submission settings...</Muted>
            </Card.Content>
        {:else if settingsQuery.isError}
            <Card.Content>
                <p class="text-destructive text-sm">Failed to load event submission settings.</p>
            </Card.Content>
        {:else}
            <Card.Content class="space-y-4">
                <div class="flex items-center justify-between gap-6 rounded-lg border p-4">
                    <div class="space-y-1">
                        <label class="text-sm font-medium" for="event-submission-enabled">Accept event submissions</label>
                        <Muted class="text-xs">
                            The deployment default is {settings?.configured_enabled ? 'enabled' : 'disabled'}. Changes apply to new requests immediately.
                        </Muted>
                    </div>
                    <Switch id="event-submission-enabled" bind:checked={eventSubmissionEnabled} disabled={updateSettings.isPending} />
                </div>

                {#if !eventSubmissionEnabled}
                    <Alert.Root variant="destructive">
                        <AlertTriangle />
                        <Alert.Title>Incoming events will be rejected with HTTP 503.</Alert.Title>
                        <Alert.Description>Session heartbeats remain successful but will not record activity while submission is disabled.</Alert.Description>
                    </Alert.Root>
                {/if}
            </Card.Content>
            <Card.Footer class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                {#if settings?.is_overridden}
                    <Button type="button" variant="outline" disabled={updateSettings.isPending} onclick={resetEventSubmission}>
                        <RotateCcw data-icon="inline-start" />
                        Reset to Deployment Default
                    </Button>
                {/if}
                <Button type="button" disabled={updateSettings.isPending || eventSubmissionEnabled === settings?.enabled} onclick={saveEventSubmission}>
                    {#if updateSettings.isPending}
                        <Spinner data-icon="inline-start" />
                        Saving...
                    {:else}
                        <Save data-icon="inline-start" />
                        Save Setting
                    {/if}
                </Button>
            </Card.Footer>
        {/if}
    </Card.Root>
</div>
