<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { writable, type Writable } from 'svelte/store';

    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import * as FacetedFilter from '$comp/faceted-filter';
    import Separator from '$comp/ui/separator/separator.svelte';

    export let title: string;
    export let value: number | undefined;
    export let open: Writable<boolean>;

    const updatedValue = writable<number | undefined>(value);

    // bind:open doesn't trigger subscriptions when the variable changes. It only updates the value of the variable.
    open.subscribe(() => updatedValue.set(value));
    $: updatedValue.set(value);

    const dispatch = createEventDispatcher();
    function onApplyFilter() {
        if ($updatedValue !== value) {
            value = $updatedValue;
            dispatch('changed', value);
        }

        open.set(false);
    }

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
            {#if value !== undefined && !isNaN(value)}
                <FacetedFilter.BadgeValue>{value}</FacetedFilter.BadgeValue>
            {:else}
                <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content class="p-0" align="start" side="bottom">
        <div class="flex items-center border-b">
            <Input type="number" placeholder={title} bind:value={$updatedValue} />
        </div>
        <FacetedFilter.Actions
            showApply={$updatedValue !== value}
            on:apply={onApplyFilter}
            showClear={$updatedValue !== undefined}
            on:clear={onClearFilter}
            on:remove={onRemoveFilter}
            on:close={() => open.set(false)}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
