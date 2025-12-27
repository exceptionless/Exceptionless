<script lang="ts">
    import type { NotificationSettings } from '$features/projects/models';

    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    interface Props {
        save: (settings: NotificationSettings) => Promise<void>;
        settings?: NotificationSettings;
    }

    let { save, settings }: Props = $props();
    let toastId = $state<number | string>();
    let errorMessage = $state<string>();

    // Local state for switch values, synced when settings prop changes
    let reportNewErrors = $state(false);
    let reportCriticalErrors = $state(false);
    let reportEventRegressions = $state(false);
    let reportNewEvents = $state(false);
    let reportCriticalEvents = $state(false);

    // Sync local state when settings prop changes
    $effect(() => {
        if (settings) {
            reportNewErrors = settings.report_new_errors ?? false;
            reportCriticalErrors = settings.report_critical_errors ?? false;
            reportEventRegressions = settings.report_event_regressions ?? false;
            reportNewEvents = settings.report_new_events ?? false;
            reportCriticalEvents = settings.report_critical_events ?? false;
        }
    });

    const debouncedSave = debounce(500, async () => {
        toast.dismiss(toastId);
        errorMessage = undefined;

        try {
            const currentSettings: NotificationSettings = {
                report_critical_errors: reportCriticalErrors,
                report_critical_events: reportCriticalEvents,
                report_event_regressions: reportEventRegressions,
                report_new_errors: reportNewErrors,
                report_new_events: reportNewEvents,
                send_daily_summary: settings?.send_daily_summary ?? false
            };
            await save(currentSettings);
        } catch {
            errorMessage = 'Error saving notification settings. Please try again.';
            toastId = toast.error(errorMessage);
        }
    });
</script>

{#if settings?.send_daily_summary !== undefined}
    <div class="space-y-3">
        <ErrorMessage message={errorMessage} />

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">New Errors</div>
                    <Muted class="text-xs">Notify me when new errors occur in this project.</Muted>
                </div>
                <Switch id="report_new_errors" bind:checked={reportNewErrors} onCheckedChange={debouncedSave} />
            </div>
        </div>

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Critical Errors</div>
                    <Muted class="text-xs">Notify me when critical errors occur in this project.</Muted>
                </div>
                <Switch id="report_critical_errors" bind:checked={reportCriticalErrors} onCheckedChange={debouncedSave} />
            </div>
        </div>

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Error Regressions</div>
                    <Muted class="text-xs">Notify me when errors regress in this project.</Muted>
                </div>
                <Switch id="report_event_regressions" bind:checked={reportEventRegressions} onCheckedChange={debouncedSave} />
            </div>
        </div>

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">New Events</div>
                    <Muted class="text-xs">Notify me when new events occur in this project.</Muted>
                </div>
                <Switch id="report_new_events" bind:checked={reportNewEvents} onCheckedChange={debouncedSave} />
            </div>
        </div>

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Critical Events</div>
                    <Muted class="text-xs">Notify me when critical events occur in this project.</Muted>
                </div>
                <Switch id="report_critical_events" bind:checked={reportCriticalEvents} onCheckedChange={debouncedSave} />
            </div>
        </div>
    </div>
{:else}
    <div class="space-y-3">
        {#each { length: 5 } as name, index (`${name}-${index}`)}
            <div class="rounded-lg border p-4">
                <div class="flex items-center justify-between">
                    <div class="space-y-1">
                        <Skeleton class="h-5 w-32 rounded" />
                        <Skeleton class="h-4 w-48 rounded" />
                    </div>
                    <Skeleton class="h-[1.15rem] w-8 rounded-full" />
                </div>
            </div>
        {/each}
    </div>
{/if}
