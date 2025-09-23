<script lang="ts">
    import type { CustomDateRange } from "$features/shared/models";

    import * as Tabs from '$comp/ui/tabs';
    import { extractRangeExpressions } from '$features/shared/utils/datemath';

    import CustomRangeForm from './custom-range-form.svelte';
    import QuickRangeSelector from './quick-range-selector.svelte';
    import { quickRanges as defaultQuickRanges, type QuickRangeSection } from './quick-ranges';

    type Props = {
        cancel?: () => void;
        class?: string;
        onselect?: (value: string) => void;
        quickRanges?: QuickRangeSection[];
        value?: Date | string;
    };

    let { cancel, class: className, onselect, quickRanges = defaultQuickRanges, value = $bindable() }: Props = $props();

    let activeTab = $state<'custom' | 'quick'>('quick');
    const parsedCustomRange = $derived(() => {
        if (!value) {
            return null;
        }

        return extractRangeExpressions(value) as CustomDateRange | null;
    });

    const quickRangeMatch = $derived(() => {
        if (!value) {
            return null;
        }

        for (const section of quickRanges) {
            for (const option of section.options) {
                if (option.value === value) {
                    return option;
                }
            }
        }

        return null;
    });

    // Auto switch tab based on current value
    $effect(() => {
        if (quickRangeMatch()) {
            activeTab = 'quick';
        } else if (value) {
            activeTab = 'custom';
        }
    });

    function handleCustomApply(range: CustomDateRange) {
        if (range.start && range.end) {
            // TODO: Format this using a shared function?
            value = `${range.start} TO ${range.end}`;
            onselect?.(value);
        }
    }
</script>

<div class={['w-[420px] p-4', className]}>
    <Tabs.Root value={activeTab}>
        <Tabs.List>
            <Tabs.Trigger value="quick">Quick Range</Tabs.Trigger>
            <Tabs.Trigger value="custom">Custom</Tabs.Trigger>
        </Tabs.List>
        <Tabs.Content value="quick">
            <QuickRangeSelector {quickRanges} bind:value {onselect} />
        </Tabs.Content>
        <Tabs.Content value="custom">
            <CustomRangeForm range={parsedCustomRange()} {cancel} apply={handleCustomApply} />
        </Tabs.Content>
    </Tabs.Root>
</div>
