<script lang="ts">
    import * as Command from '$comp/ui/command';

    import { quickRanges as defaultQuickRanges, type QuickRangeOption, type QuickRangeSection } from './quick-ranges';

    type Props = {
        onselect?: (value: string) => void;
        quickRanges?: QuickRangeSection[];
        value?: Date | string;
    };

    let { onselect, quickRanges = defaultQuickRanges, value = $bindable() }: Props = $props();

    function selectQuick(option: QuickRangeOption) {
        value = option.value;
        onselect?.(option.value);
    }
</script>

<div class="space-y-3">
    <Command.Root class="rounded-md border">
        <Command.Input placeholder="Search quick ranges" />
        <Command.List class="max-h-56 overflow-y-auto">
            <Command.Empty>No matching quick ranges</Command.Empty>
            {#each quickRanges as section (section.label)}
                <Command.Group heading={section.label}>
                    {#each section.options as option (option.value)}
                        <Command.Item
                            value={option.label}
                            onclick={() => selectQuick(option)}
                            aria-selected={option.value === value}
                            class={['cursor-pointer', option.value === value ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible']}
                        >
                            {option.label}
                        </Command.Item>
                    {/each}
                </Command.Group>
            {/each}
        </Command.List>
    </Command.Root>
</div>
