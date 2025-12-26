<script lang="ts">
    import type { NotificationSettings } from '$features/projects/models';

    import ErrorMessage from '$comp/error-message.svelte';
    import { A, H4, Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import InfoIcon from '@lucide/svelte/icons/info';
    import { toast } from 'svelte-sonner';
    import { debounce } from 'throttle-debounce';

    interface Props {
        emailNotificationsEnabled?: boolean;
        hasPremiumFeatures?: boolean;
        save: (settings: NotificationSettings) => Promise<void>;
        settings?: NotificationSettings;
        upgrade: () => Promise<void>;
    }

    let { emailNotificationsEnabled = true, hasPremiumFeatures = false, save, settings, upgrade }: Props = $props();
    let toastId = $state<number | string>();
    let errorMessage = $state<string>();

    // Local state for switch values, synced when settings prop changes
    let sendDailySummary = $state(false);
    let reportNewErrors = $state(false);
    let reportCriticalErrors = $state(false);
    let reportEventRegressions = $state(false);
    let reportNewEvents = $state(false);
    let reportCriticalEvents = $state(false);

    // Sync local state when settings prop changes
    $effect(() => {
        if (settings) {
            sendDailySummary = settings.send_daily_summary ?? false;
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
                send_daily_summary: sendDailySummary
            };
            await save(currentSettings);
        } catch {
            errorMessage = 'Error saving notification settings. Please try again.';
            toastId = toast.error(errorMessage);
        }
    });
</script>

{#if settings?.send_daily_summary !== undefined}
    <div class="space-y-6">
        <ErrorMessage message={errorMessage} />

        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div>
                    <div class="text-sm font-medium">Daily Project Summary</div>
                    <Muted class="text-xs">Receive a daily summary of your project activity.</Muted>
                </div>
                <Switch id="send_daily_summary" bind:checked={sendDailySummary} onCheckedChange={debouncedSave} disabled={!emailNotificationsEnabled} />
            </div>
        </div>

        <div class="space-y-4">
            <H4>Event Notifications</H4>
            <div class="space-y-3">
                {#if !hasPremiumFeatures}
                    <Alert.Root variant="information">
                        <InfoIcon />
                        <Alert.Title>
                            <A onclick={upgrade}>Upgrade now</A> to enable occurrence level notifications!
                        </Alert.Title>
                    </Alert.Root>
                {/if}
                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">New Errors</div>
                            <Muted class="text-xs">Notify me when new errors occur in this project.</Muted>
                        </div>
                        <Switch
                            id="report_new_errors"
                            bind:checked={reportNewErrors}
                            onCheckedChange={debouncedSave}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>

                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">Critical Errors</div>
                            <Muted class="text-xs">Notify me when critical errors occur in this project.</Muted>
                        </div>
                        <Switch
                            id="report_critical_errors"
                            bind:checked={reportCriticalErrors}
                            onCheckedChange={debouncedSave}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>

                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">Error Regressions</div>
                            <Muted class="text-xs">Notify me when errors regress in this project.</Muted>
                        </div>
                        <Switch
                            id="report_event_regressions"
                            bind:checked={reportEventRegressions}
                            onCheckedChange={debouncedSave}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>

                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">New Events</div>
                            <Muted class="text-xs">Notify me when new events occur in this project.</Muted>
                        </div>
                        <Switch
                            id="report_new_events"
                            bind:checked={reportNewEvents}
                            onCheckedChange={debouncedSave}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>

                <div class="rounded-lg border p-4" class:opacity-60={!hasPremiumFeatures}>
                    <div class="flex items-center justify-between">
                        <div>
                            <div class="text-sm font-medium">Critical Events</div>
                            <Muted class="text-xs">Notify me when critical events occur in this project.</Muted>
                        </div>
                        <Switch
                            id="report_critical_events"
                            bind:checked={reportCriticalEvents}
                            onCheckedChange={debouncedSave}
                            disabled={!emailNotificationsEnabled || !hasPremiumFeatures}
                        />
                    </div>
                </div>
            </div>
        </div>
    </div>
{:else}
    <div class="space-y-6">
        <div class="rounded-lg border p-4">
            <div class="flex items-center justify-between">
                <div class="space-y-1">
                    <Skeleton class="h-5 w-32 rounded" />
                    <Skeleton class="h-4 w-48 rounded" />
                </div>
                <Skeleton class="h-[1.15rem] w-8 rounded-full" />
            </div>
        </div>

        <div class="space-y-4">
            <Skeleton class="h-6 w-40 rounded" />
            <div class="space-y-3">
                {#each { length: 5 } as name, index (`${name}-${index}`)}
                    <div class="rounded-lg border p-4 opacity-60">
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
        </div>
    </div>
{/if}
