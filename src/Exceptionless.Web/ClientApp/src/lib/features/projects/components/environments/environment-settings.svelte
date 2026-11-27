<script lang="ts">
    import type { UpdateProjectEnvironmentSettings } from '#features/projects/models.ts';

    import { Checkbox } from '#comp/ui/checkbox/index.ts';
    import { Label } from '#comp/ui/label/index.ts';
    import * as RadioGroup from '#comp/ui/radio-group/index.ts';

    interface Props {
        disabled?: boolean;
        discardEventsWithoutEnvironment: boolean;
        discardUnlistedEnvironments: boolean;
        update: (settings: UpdateProjectEnvironmentSettings) => Promise<unknown> | unknown;
    }

    let { disabled = false, discardEventsWithoutEnvironment, discardUnlistedEnvironments, update }: Props = $props();
</script>

<div class="space-y-3">
    <Label id="accept-events-label">Accept events from</Label>
    <RadioGroup.Root
        aria-labelledby="accept-events-label"
        class="gap-3"
        {disabled}
        onValueChange={(value) =>
            update({
                discard_unlisted_environments: value === 'listed'
            })}
        value={discardUnlistedEnvironments ? 'listed' : 'any'}
    >
        <div class="flex items-start gap-3">
            <RadioGroup.Item id="accept-any-environment" value="any" />
            <div class="space-y-0.5">
                <Label class="font-normal" for="accept-any-environment">Any environment</Label>
                <p class="text-muted-foreground text-sm">New environments show up in the table above as they are seen.</p>
            </div>
        </div>
        <div class="flex items-start gap-3">
            <RadioGroup.Item id="accept-listed-environments" value="listed" />
            <div class="space-y-0.5">
                <Label class="font-normal" for="accept-listed-environments">Only environments in this list</Label>
                <p class="text-muted-foreground text-sm">Others are discarded and don't count toward your plan limit.</p>
            </div>
        </div>
    </RadioGroup.Root>

    {#if discardUnlistedEnvironments}
        <div class="ml-7 flex items-center gap-2">
            <Checkbox
                checked={!discardEventsWithoutEnvironment}
                {disabled}
                id="accept-events-without-environment"
                onCheckedChange={(checked) =>
                    update({
                        discard_events_without_environment: !checked
                    })}
            />
            <Label class="font-normal" for="accept-events-without-environment">Also accept events that don't send an environment</Label>
        </div>
    {/if}
</div>
