<script lang="ts">
    import type { Snippet } from 'svelte';

    import { Button } from '$comp/ui/button';
    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import Circle from 'lucide-svelte/icons/circle-plus';

    import type { FacetedFilter, IFilter } from './models';

    import { builderContext, type FacetFilterBuilder } from './faceted-filter-builder-context.svelte';

    interface Props {
        changed: (filter: IFilter) => void;
        children?: Snippet;
        filters: IFilter[];
        remove: (filter?: IFilter) => void;
    }

    let { changed, children, filters, remove }: Props = $props();

    let open = $state(false);
    let lastOpenFilterId = $state<string>();
    let facets: FacetedFilter<IFilter>[] = $derived(
        filters.map((filter) => {
            const builder = builderContext.get(filter.key);
            if (!builder) {
                throw new Error(`Facet filter builder not found for key: ${filter.key}`);
            }

            const f = builder.create(filter);
            return {
                component: builder.component,
                filter: f,
                open: lastOpenFilterId === f.id,
                title: builder.title
            };
        })
    );

    const sortedBuilders = $derived(
        [...builderContext.entries()].sort((facetA, facetB) => {
            const priorityA = facetA[1].priority ?? 0;
            const priorityB = facetB[1].priority ?? 0;
            if (priorityA !== priorityB) {
                return priorityB - priorityA;
            }

            return facetA[1].title.localeCompare(facetB[1].title);
        })
    );

    function onFacetSelected(builder: FacetFilterBuilder<IFilter>) {
        facets.forEach((f) => (f.open = false));

        const filter = builder.create();
        changed(filter);

        open = false;
        lastOpenFilterId = filter.id;
    }

    function filterChanged(filter: IFilter) {
        changed(filter);
    }

    function filterRemoved(filter: IFilter) {
        if (lastOpenFilterId === filter.id) {
            lastOpenFilterId = undefined;
        }

        remove(filter);
    }

    function onRemoveAll() {
        lastOpenFilterId = undefined;
        facets.forEach((facet) => facet.filter.reset());
        remove();
    }

    function onClose() {
        open = false;
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger>
        {#snippet children()}
            <Button class="h-8" size="sm" variant="outline">
                <Circle class="mr-2 size-4" /> Filter
            </Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" class="w-[200px] p-0" side="bottom">
        <Command.Root>
            <Command.Input placeholder={'Search...'} />
            <Command.List>
                <Command.Empty>No results found.</Command.Empty>
                <Command.Group>
                    {#each sortedBuilders as [key, builder]}
                        <Command.Item onSelect={() => onFacetSelected(builder)} value={key}>{builder.title}</Command.Item>
                    {/each}
                </Command.Group>
            </Command.List>
        </Command.Root>
        <Command.Root>
            <Command.List>
                <Command.Separator />
                {#if facets.length > 0}
                    <Command.Item class="justify-center text-center" onSelect={onRemoveAll}>Clear filters</Command.Item>
                {/if}
                <Command.Item class="justify-center text-center" onSelect={onClose}>Close</Command.Item>
            </Command.List>
        </Command.Root>
    </Popover.Content>
</Popover.Root>

{#if children}
    {@render children()}
{/if}

{#each facets as facet (facet.filter.id)}
    {@const Facet = facet.component}
    <Facet filter={facet.filter} {filterChanged} {filterRemoved} open={facet.open} title={facet.title} />
{/each}
