<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { writable } from 'svelte/store';

    import { Button } from '$comp/ui/button';
    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import * as FacetedFilter from '$comp/faceted-filter';
    import Separator from '$comp/ui/separator/separator.svelte';

    export let title: string = 'Keyword';
    export let value: string;

    const updatedValue = writable<string>(value);
    const open = writable<boolean>(false);
    open.subscribe(($open) => {
        if ($open) {
            updatedValue.set(value);
        } else if ($updatedValue !== value) {
            value = $updatedValue;
            dispatch('changed', value);
        }
    });

    const dispatch = createEventDispatcher();
    export function onClearFilter() {
        updatedValue.set('');
    }

    function onRemoveFilter(): void {
        value = '';
        dispatch('remove');
    }
</script>

<Popover.Root bind:open={$open}>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8 border-dashed">
            {title}
            <Separator orientation="vertical" class="mx-2 h-4" />
            {#if value}
                <FacetedFilter.BadgeValue>{value}</FacetedFilter.BadgeValue>
            {:else}
                <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content class="p-0" align="start" side="bottom">
        <Command.Root filter={() => 1}>
            <Command.Input placeholder={title} bind:value={$updatedValue} />
        </Command.Root>
        <FacetedFilter.Actions
            showApply={$updatedValue !== value}
            onApply={() => open.set(false)}
            showClear={!!$updatedValue?.trim()}
            onClear={onClearFilter}
            onRemove={onRemoveFilter}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
