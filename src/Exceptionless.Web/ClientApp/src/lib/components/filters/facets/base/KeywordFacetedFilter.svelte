<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { writable } from 'svelte/store';

    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import * as FacetedFilter from '$comp/faceted-filter';
    import Separator from '$comp/ui/separator/separator.svelte';

    export let title: string = 'Keyword';
    export let value: string | undefined;

    const updatedValue = writable<string | undefined>(value);
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
        updatedValue.set(undefined);
    }

    function onRemoveFilter(): void {
        value = undefined;
        dispatch('remove');
    }
</script>

<Popover.Root bind:open={$open}>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
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
        <div class="flex items-center border-b">
            <Input type="text" placeholder={title} bind:value={$updatedValue} />
        </div>
        <FacetedFilter.Actions
            showApply={$updatedValue !== value}
            on:apply={() => open.set(false)}
            showClear={!!$updatedValue?.trim()}
            on:clear={onClearFilter}
            on:remove={onRemoveFilter}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
