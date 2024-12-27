<script lang="ts">
    import * as Select from '$comp/ui/select';

    import { StackStatus } from '../models';

    interface Props {
        onChange?: (status: StackStatus) => Promise<void>;
        value: StackStatus;
    }

    let { onChange, value }: Props = $props();

    type Item = { label: string; value: StackStatus };
    const items: Item[] = [
        { label: 'Open', value: StackStatus.Open },
        { label: 'Fixed', value: StackStatus.Fixed },
        { label: 'Regressed', value: StackStatus.Regressed },
        { label: 'Snoozed', value: StackStatus.Snoozed },
        { label: 'Ignored', value: StackStatus.Ignored },
        { label: 'Discarded', value: StackStatus.Discarded }
    ];

    let selected = $derived((items.find((item) => item.value === value) || items[items.length - 1]) as Item);
    async function onValueChange(status: string) {
        await onChange?.(status as StackStatus);
    }
</script>

<Select.Root type="single" {items} {value} {onValueChange}>
    <Select.Trigger class="w-[135px]">
        {selected.label}
    </Select.Trigger>
    <Select.Content>
        {#each items as item (item.label)}
            <Select.Item value={item.value}>{item.label}</Select.Item>
        {/each}
    </Select.Content>
</Select.Root>
