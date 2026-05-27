<script lang="ts">
    import type { KeywordFilter } from '$features/events/components/filters';
    import type { Snippet } from 'svelte';

    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';
    import Circle from '@lucide/svelte/icons/circle-plus';
    import { computeCommandScore } from 'bits-ui';

    import type { FacetedFilter, IFilter } from './models';

    import { builderContext, type FacetFilterBuilder } from './faceted-filter-builder-context.svelte';

    interface Props {
        changed: (filter: IFilter) => void;
        children?: Snippet;
        filters: IFilter[];
        remove: (filter?: IFilter) => void;
    }

    let { changed, children, filters, remove }: Props = $props();

    const CREATE_KEYWORD_FILTER_COMMAND_ITEM = 'CREATE_KEYWORD_FILTER_COMMAND_ITEM';
    let open = $state(false);
    let lastOpenFilterId = $state<string>();
    let search = $state('');
    let showHiddenFilters = $state(false);

    // Clear the builder context because multiple builders will be loaded during page navigation.
    builderContext.clear();
    let facets: FacetedFilter<IFilter>[] = $state([]);

    $effect.pre(() => {
        if (builderContext.size === 0) {
            facets = [];
            return;
        }

        const newFacets = filters
            .map((filter) => {
                const builder = builderContext.get(filter.key);
                if (!builder) {
                    return;
                }

                const f = builder.create(filter);
                // Reuse existing facet to preserve open state and avoid component recreation
                const existing = facets.find((facet) => facet.filter.id === f.id);
                if (existing) {
                    existing.filter = f;
                    existing.component = builder.component;
                    existing.title = builder.title;
                    return existing;
                }
                return {
                    component: builder.component,
                    filter: f,
                    open: lastOpenFilterId === f.id,
                    title: builder.title
                };
            })
            .filter((f): f is FacetedFilter<IFilter> => !!f);

        // Only replace the array if the set of facets actually changed
        const idsMatch =
            newFacets.length === facets.length && newFacets.every((f, i) => f.filter.id === facets[i]?.filter.id);
        if (!idsMatch) {
            facets = newFacets;
        }
    });

    const hiddenFilterCount = $derived(filters.filter((filter) => filter.hidden).length);
    const hiddenFilterLabel = $derived(`${hiddenFilterCount} Hidden ${hiddenFilterCount === 1 ? 'Filter' : 'Filters'}`);
    const hasFilters = $derived(filters.length > 0);
    const visibleFacets = $derived(facets.filter((facet) => !facet.filter.hidden || showHiddenFilters));

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
        remove();
    }

    function onClose() {
        open = false;
    }

    function toggleHiddenFilters() {
        showHiddenFilters = !showHiddenFilters;
        open = false;
    }

    function onOpenChange(isOpen: boolean) {
        if (!isOpen) {
            onClose();
        }
    }

    function onCreateKeywordFromSearch() {
        const value = search.trim();
        if (!value) {
            return;
        }

        // If an existing keyword filter matches exactly, open it instead of creating a new one.
        const existingKeywordFilter = filters.find((f) => f.key === 'keyword' && (f as KeywordFilter).value === value) as KeywordFilter | undefined;

        if (existingKeywordFilter) {
            open = false;
            search = '';
            lastOpenFilterId = existingKeywordFilter.id;
            return;
        }

        const keywordBuilder = builderContext.get('keyword');
        if (keywordBuilder) {
            const filter = keywordBuilder.create() as KeywordFilter;
            filter.value = value;
            changed(filter);

            open = false;
            search = '';
            lastOpenFilterId = filter.id;
        }
    }

    function filterCommand(commandValue: string, searchInput: string, commandKeywords?: string[]) {
        if (commandValue === CREATE_KEYWORD_FILTER_COMMAND_ITEM) {
            return 1; // Always visible
        }

        // For builder keys, include the builder title as keywords for better searchability
        const builder = builderContext.get(commandValue);
        const keywords = builder ? [builder.title, ...(commandKeywords ?? [])] : commandKeywords;

        return computeCommandScore(commandValue, searchInput, keywords);
    }
</script>

{#if children}
    {@render children()}
{/if}

{#each visibleFacets as facet (facet.filter.id)}
    {@const Facet = facet.component}
    <div class:opacity-70={facet.filter.hidden}>
        <Facet
            filter={facet.filter}
            {filterChanged}
            {filterRemoved}
            bind:open={
                () => facet.open,
                (isOpen) => {
                    lastOpenFilterId = isOpen ? facet.filter.id : undefined;
                    facet.open = isOpen;
                }
            }
            title={facet.title}
        />
    </div>
{/each}

<Popover.Root bind:open {onOpenChange}>
    <Popover.Trigger>
        {#snippet child({ props })}
            <Button
                {...props}
                class={['relative', !hasFilters && 'gap-x-1 px-3']}
                size={hasFilters ? 'icon-lg' : 'lg'}
                variant="outline"
                title="Add and Manage Filters"
                aria-label={hiddenFilterCount > 0 ? `Add and Manage Filters. ${hiddenFilterLabel}.` : 'Add and Manage Filters'}
            >
                <Circle class={[hasFilters ? 'size-4' : 'mr-2 size-4']} aria-hidden="true" />
                {#if hiddenFilterCount > 0}
                    <Badge
                        variant="secondary"
                        class="absolute -top-1 -right-1 h-4 min-w-4 rounded-full px-1 text-[10px] leading-none shadow-sm"
                        aria-hidden="true"
                    >
                        {hiddenFilterCount}
                    </Badge>
                {/if}
                {#if !hasFilters}
                    Filter
                {/if}
            </Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" class="w-65 p-0" side="bottom" trapFocus={false}>
        <Command.Root filter={filterCommand}>
            <Command.Input placeholder="Search..." bind:value={search} />
            <Command.List>
                <Command.Group>
                    {#each sortedBuilders as [key, builder] (key)}
                        <Command.Item value={key} onSelect={() => onFacetSelected(builder)}>
                            {builder.title}
                        </Command.Item>
                    {/each}
                </Command.Group>
                {#if search}
                    <Command.Group>
                        <Command.Item value={CREATE_KEYWORD_FILTER_COMMAND_ITEM} onSelect={onCreateKeywordFromSearch}>
                            Create Keyword Filter: "{search}"
                        </Command.Item>
                    </Command.Group>
                {/if}
            </Command.List>
        </Command.Root>
        <div class="flex flex-col">
            <Separator />
            {#if hiddenFilterCount > 0}
                <Button variant="ghost" onclick={toggleHiddenFilters}>
                    {showHiddenFilters ? 'Hide Hidden Filters' : `Show ${hiddenFilterLabel}`}
                </Button>
            {/if}
            {#if filters.some((f) => f.type !== 'date')}
                <Button variant="ghost" onclick={onRemoveAll}>Clear Filters</Button>
            {/if}
        </div>
    </Popover.Content>
</Popover.Root>
