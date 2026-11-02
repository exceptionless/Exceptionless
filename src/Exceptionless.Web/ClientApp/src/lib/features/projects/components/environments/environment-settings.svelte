<script lang="ts">
    import type { UpdateProjectEnvironmentSettings } from '#features/projects/models.ts';

    import { Label } from '#comp/ui/label/index.ts';
    import { Switch } from '#comp/ui/switch/index.ts';

    interface Props {
        disabled?: boolean;
        discardEventsWithoutEnvironment: boolean;
        discardUnlistedEnvironments: boolean;
        notifyOtherEnvironments: boolean;
        update: (settings: UpdateProjectEnvironmentSettings) => Promise<unknown> | unknown;
    }

    let { disabled = false, discardEventsWithoutEnvironment, discardUnlistedEnvironments, notifyOtherEnvironments, update }: Props = $props();
</script>

<div class="space-y-4">
    <div class="flex items-start gap-3">
        <Switch
            id="discard-unlisted-environments"
            {disabled}
            checked={discardUnlistedEnvironments}
            onCheckedChange={(checked) =>
                update({
                    discard_unlisted_environments: checked
                })}
        />
        <div class="space-y-1">
            <Label for="discard-unlisted-environments">Discard events from environments that are not listed</Label>
            <p class="text-muted-foreground text-sm">
                Events whose environment is not in the list are dropped before they are stored, so they never create stacks. They count as discarded events in
                your usage, not toward your plan limit. Matching ignores case.
            </p>
        </div>
    </div>

    <div class="ml-9 flex items-start gap-3">
        <Switch
            id="discard-events-without-environment"
            disabled={disabled || !discardUnlistedEnvironments}
            checked={discardEventsWithoutEnvironment}
            onCheckedChange={(checked) =>
                update({
                    discard_events_without_environment: checked
                })}
        />
        <div class="space-y-1">
            <Label for="discard-events-without-environment">Also discard events that have no environment</Label>
            <p class="text-muted-foreground text-sm">
                Only applies while events from unlisted environments are discarded. Clients that do not send an environment will stop being recorded.
            </p>
        </div>
    </div>

    <div class="flex items-start gap-3">
        <Switch
            id="notify-other-environments"
            {disabled}
            checked={notifyOtherEnvironments}
            onCheckedChange={(checked) =>
                update({
                    notify_other_environments: checked
                })}
        />
        <div class="space-y-1">
            <Label for="notify-other-environments">Send notifications for other environments</Label>
            <p class="text-muted-foreground text-sm">Covers environments that are not listed and events without an environment.</p>
        </div>
    </div>
</div>
