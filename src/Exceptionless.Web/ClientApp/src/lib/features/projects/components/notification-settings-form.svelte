<script lang="ts">
    import type { NotificationSettings } from '$features/projects/models';

    import { Label } from '$comp/ui/label';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';

    interface Props {
        changed: (settings: NotificationSettings) => Promise<void>;
        settings?: NotificationSettings;
    }

    // TODO: Clone settings?
    // TODO: Add Skeletons
    // TODO: Use switch primitive in shared?
    let { changed, settings }: Props = $props();
</script>

{#if settings}
    <div class="flex items-center space-x-2">
        <Switch id="send_daily_summary" bind:checked={settings.send_daily_summary} onCheckedChange={async () => await changed(settings)} />
        <Label for="send_daily_summary">
            Send daily project summary <strong>(Coming soon!)</strong>
        </Label>
    </div>

    <div class="flex items-center space-x-2">
        <Switch id="report_new_errors" bind:checked={settings.report_new_errors} onCheckedChange={async () => await changed(settings)} />
        <Label for="report_new_errors">Notify me on new errors</Label>
    </div>

    <div class="flex items-center space-x-2">
        <Switch id="report_critical_errors" bind:checked={settings.report_critical_errors} onCheckedChange={async () => await changed(settings)} />
        <Label for="report_critical_errors">Notify me on critical errors</Label>
    </div>

    <div class="flex items-center space-x-2">
        <Switch id="report_event_regressions" bind:checked={settings.report_event_regressions} onCheckedChange={async () => await changed(settings)} />
        <Label for="report_event_regressions">Notify me on error regressions</Label>
    </div>

    <div class="flex items-center space-x-2">
        <Switch id="report_new_events" bind:checked={settings.report_new_events} onCheckedChange={async () => await changed(settings)} />
        <Label for="report_new_events">Notify me on new events</Label>
    </div>

    <div class="flex items-center space-x-2">
        <Switch id="report_critical_events" bind:checked={settings.report_critical_events} onCheckedChange={async () => await changed(settings)} />
        <Label for="report_critical_events">Notify me on critical events</Label>
    </div>
{:else}
    <Skeleton />
{/if}
