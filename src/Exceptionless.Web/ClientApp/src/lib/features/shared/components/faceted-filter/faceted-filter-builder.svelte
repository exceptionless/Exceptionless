<script lang="ts">
    import type { KeywordFilter } from '$features/events/components/filters';
    import type { Snippet } from 'svelte';

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

    // Clear the builder context because multiple builders will be loaded during page navigation.
    builderContext.clear();
    let facets: FacetedFilter<IFilter>[] = $derived.by(() => {
        if (builderContext.size === 0) {
            return [];
        }

        return filters
            .map((filter) => {
                const builder = builderContext.get(filter.key);
                if (!builder) {
                    return;
                }

                const f = builder.create(filter);
                return {
                    component: builder.component,
                    filter: f,
                    open: lastOpenFilterId === f.id,
                    title: builder.title
                };
            })
            .filter((f): f is FacetedFilter<IFilter> => !!f);
    });

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
        if (lastOpenFilterId === filter.id) {
            lastOpenFilterId = undefined;
        }

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

<Popover.Root bind:open {onOpenChange}>
    <Popover.Trigger>
        {#snippet child({ props })}
            <Button {...props} class="gap-x-1 px-3" size="lg" variant="outline">
                <Circle class="mr-2 size-4" /> Filter
            </Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" class="w-[260px] p-0" side="bottom" trapFocus={false}>
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
                            Create keyword filter: "{search}"
                        </Command.Item>
                    </Command.Group>
                {/if}
            </Command.List>
        </Command.Root>
        <div class="flex flex-col">
            <Separator />
            {#if filters.some((f) => f.type !== 'date')}
                <Button class="justify-center text-center" variant="ghost" onclick={onRemoveAll}>Clear filters</Button>
            {/if}
            <Button class="justify-center text-center" variant="ghost" onclick={onClose}>Close</Button>
        </div>
    </Popover.Content>
</Popover.Root>

{#if children}
    {@render children()}
{/if}

{#each facets as facet (facet.filter.id)}
    {@const Facet = facet.component}
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
{/each}
