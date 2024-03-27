<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import type { Readable } from 'svelte/store';
    import { toast } from 'svelte-sonner';

    import IconAddCircleOutline from '~icons/mdi/add-circle-outline';

    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import { Button } from '$comp/ui/button';
    import type { IFilter } from '$comp/filters/filters';
    import type { FacetedFilter } from '.';

    const dispatch = createEventDispatcher();

    export let facets: Readable<FacetedFilter[]>;

    let open = false;
    let visible: string[] = [];
    facets.subscribe(($facets) => {
        // Add any new facets that have been synced from storage.
        visible = [...visible, ...$facets.filter((f) => !f.filter.isEmpty() && !visible.includes(f.filter.key)).map((f) => f.filter.key)];
    });

    function onFacetSelected(facet: FacetedFilter) {
        if (visible.includes(facet.filter.key)) {
            toast.error(`Only one ${facet.title} filter can be applied at a time.`);
        } else {
            visible = [...visible, facet.filter.key];
        }
    }

    function onChanged({ detail }: CustomEvent<IFilter>) {
        onFilterChanged(detail);
    }

    function onFilterChanged(filter?: IFilter) {
        dispatch('changed', filter);
    }

    function onRemove({ detail }: CustomEvent<IFilter>) {
        visible = visible.filter((key) => key !== detail.key);

        if (!detail.isEmpty()) {
            detail.reset();
        }

        dispatch('remove', detail);
    }

    function onRemoveAll() {
        visible = [];
        if ($facets.every((f) => f.filter.isEmpty())) {
            return;
        }

        $facets.forEach((facet) => facet.filter.reset());
        dispatch('remove');
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
            <IconAddCircleOutline class="mr-2 h-4 w-4" /> Filter
        </Button>
    </Popover.Trigger>
    <Popover.Content class="w-[200px] p-0" align="start" side="bottom">
        <Command.Root>
            <Command.Input placeholder={'Search...'} />
            <Command.List>
                <Command.Empty>No results found.</Command.Empty>
                <Command.Group>
                    {#each $facets as facet (facet.filter.key)}
                        <Command.Item value={facet.filter.key} onSelect={() => onFacetSelected(facet)}>{facet.title}</Command.Item>
                    {/each}
                </Command.Group>
                {#if visible.length > 0}
                    <Command.Separator />
                    <Command.Item class="justify-center text-center" onSelect={onRemoveAll}>Clear filters</Command.Item>
                {/if}
            </Command.List>
        </Command.Root>
    </Popover.Content>
</Popover.Root>

{#each $facets as facet (facet.filter.key)}
    {#if visible.includes(facet.filter.key)}
        <svelte:component this={facet.component} filter={facet.filter} title={facet.title} on:changed={onChanged} on:remove={onRemove} />
    {/if}
{/each}
