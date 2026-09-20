<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import { getTagSuggestionsQuery } from '$features/events/api.svelte';
    import { TAG_SUGGESTION_LIMIT, tagSuggestions } from '$features/events/tag-suggestions';
    import { organization } from '$features/organizations/context.svelte';

    import { TagFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, open = $bindable(false), title = 'Tag', ...props }: FacetedFilterProps<TagFilter> = $props();
    let search = $state('');
    let debouncedSearch = $state('');
    const normalizedSearch = $derived(search.trim().toLowerCase());

    const initialQuery = getTagSuggestionsQuery({
        enabled: () => open,
        params: {
            search: ''
        },
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const initial = $derived(tagSuggestions(initialQuery.data));
    const searchQuery = getTagSuggestionsQuery({
        enabled: () => open && initialQuery.isSuccess && !initial.complete && debouncedSearch.length >= 2 && debouncedSearch === normalizedSearch,
        params: {
            get search() {
                return debouncedSearch;
            }
        },
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const remoteSearch = $derived(!initial.complete && normalizedSearch.length >= 2);
    const currentSearch = $derived(debouncedSearch === normalizedSearch);
    const result = $derived(remoteSearch && currentSearch && searchQuery.isSuccess ? tagSuggestions(searchQuery.data) : initial);
    const options = $derived(
        Array.from(new Set(['Critical', ...filter.value, ...result.tags]))
            .filter((tag) => tag.toLowerCase().includes(normalizedSearch))
            .slice(0, TAG_SUGGESTION_LIMIT)
            .map((tag) => ({
                label: tag,
                value: tag
            }))
    );
    const loading = $derived(open && (initialQuery.isFetching || (remoteSearch && (!currentSearch || searchQuery.isFetching))));
    const failed = $derived(initialQuery.isError || (remoteSearch && currentSearch && searchQuery.isError));

    const statusMessage = $derived.by(() => {
        if (loading) {
            return 'Searching tags…';
        }

        if (!initial.complete && normalizedSearch.length < 2) {
            return 'Showing up to 250 tags. Type at least two characters to search all tags.';
        }

        if (options.length === 0) {
            return 'No matching tags found.';
        }

        if (remoteSearch && result.tags.length === TAG_SUGGESTION_LIMIT) {
            return 'Showing up to 250 tags. Type more to narrow.';
        }
        return undefined;
    });

    $effect(() => {
        const value = normalizedSearch;
        if (!open) {
            debouncedSearch = '';
            return;
        }
        const timer = setTimeout(() => {
            debouncedSearch = value;
        }, 300);
        return () => clearTimeout(timer);
    });

    function toggleHidden() {
        filter.hidden = !filter.hidden;
        filterChanged(filter);
    }
</script>

<FacetedFilter.MultiSelect
    bind:open
    bind:search
    shouldFilter={false}
    changed={(values: string[]) => {
        filter.value = values;
        filterChanged(filter);
    }}
    {loading}
    {options}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    hidden={filter.hidden}
    {title}
    {toggleHidden}
    values={filter.value}
    {...props}
>
    {#snippet status()}
        {#if failed || statusMessage}
            <div class="text-muted-foreground px-3 py-2 text-xs" role="status">
                {#if failed}
                    Could not load tags.
                    <Button
                        size="sm"
                        variant="link"
                        onclick={() => {
                            void (initialQuery.isError ? initialQuery.refetch() : searchQuery.refetch());
                        }}>Retry</Button
                    >
                {:else}
                    {statusMessage}
                {/if}
            </div>
        {/if}
    {/snippet}
</FacetedFilter.MultiSelect>
