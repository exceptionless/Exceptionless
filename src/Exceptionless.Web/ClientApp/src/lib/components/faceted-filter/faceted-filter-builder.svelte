<script lang="ts">
    import type { FacetedFilter } from '$comp/filters/facets';
    import type { IFilter } from '$comp/filters/filters.svelte';

    import { Button } from '$comp/ui/button';
    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import IconAddCircleOutline from '~icons/mdi/add-circle-outline';
    import { toast } from 'svelte-sonner';

    interface Props {
        changed: (filter: IFilter) => void;
        facets: FacetedFilter<IFilter>[];
        remove: (filter?: IFilter) => void;
    }

    let { changed, facets, remove }: Props = $props();

    let open = $state(false);
    let visible = $state<string[]>([]);

    function onFacetSelected(facet: FacetedFilter<IFilter>) {
        facets.forEach((f) => (f.open = false));

        if (visible.includes(facet.filter.key)) {
            toast.error(`Only one ${facet.title} filter can be applied at a time.`);
        } else {
            visible = [...visible, facet.filter.key];
        }

        open = false;
        facet.open = true;
    }

    function filterChanged(filter: IFilter) {
        changed(filter);
    }

    function filterRemoved(filter: IFilter) {
        visible = visible.filter((key) => key !== filter.key);

        if (!filter.isEmpty()) {
            filter.reset();
        }

        remove(filter);
    }

    function onRemoveAll() {
        visible = [];
        facets.forEach((facet) => facet.filter.reset());
        remove();
    }

    function onClose() {
        open = false;
    }

    function isVisible(facet: FacetedFilter<IFilter>): boolean {
        // Add any new facets that have been synced from storage.
        const visibleFacets = [...visible, ...facets.filter((f) => !f.filter.isEmpty() && !visible.includes(f.filter.key)).map((f) => f.filter.key)];
        return visibleFacets.includes(facet.filter.key);
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} class="h-8" size="sm" variant="outline">
            <IconAddCircleOutline class="mr-2 h-4 w-4" /> Filter
        </Button>
    </Popover.Trigger>
    <Popover.Content align="start" class="w-[200px] p-0" side="bottom">
        <Command.Root>
            <Command.Input placeholder={'Search...'} />
            <Command.List>
                <Command.Empty>No results found.</Command.Empty>
                <Command.Group>
                    {#each facets as facet (facet.filter.key)}
                        <Command.Item onSelect={() => onFacetSelected(facet)} value={facet.filter.key}>{facet.title}</Command.Item>
                    {/each}
                </Command.Group>
            </Command.List>
        </Command.Root>
        <Command.Root>
            <Command.List>
                <Command.Separator />
                {#if visible.length > 0}
                    <Command.Item class="justify-center text-center" onSelect={onRemoveAll}>Clear filters</Command.Item>
                {/if}
                <Command.Item class="justify-center text-center" onSelect={onClose}>Close</Command.Item>
            </Command.List>
        </Command.Root>
    </Popover.Content>
</Popover.Root>

{#each facets as facet (facet.filter.key)}
    {#if isVisible(facet)}
        <svelte:component this={facet.component} filter={facet.filter} {filterChanged} {filterRemoved} open={facet.open} title={facet.title} />
    {/if}
{/each}
