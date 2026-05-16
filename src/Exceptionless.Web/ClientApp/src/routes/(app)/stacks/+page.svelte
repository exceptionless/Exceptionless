<script lang="ts">
    import * as DataTable from '$comp/data-table';
    import RefreshButton from '$comp/refresh-button.svelte';
    import { H3 } from '$comp/typography';
    import { getStacksQuery, type GetStacksParams } from '$features/stacks/api.svelte';
    import type { Stack } from '$features/stacks/models';
    import { organization } from '$features/organizations/context.svelte';
    import { queryParamsState } from 'kit-query-params';
    import { watch } from 'runed';

    const DEFAULT_PARAMS = {
        filter: 'status:open',
        limit: 25,
        sort: '-last_occurrence',
        time: '[now-7d TO now]'
    };

    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number',
            sort: 'string',
            time: 'string'
        }
    });

    watch(
        () => organization.current,
        () => {
            Object.assign(queryParams, DEFAULT_PARAMS);
        },
        { lazy: true }
    );

    let params: GetStacksParams = $derived({
        filter: queryParams.filter,
        sort: queryParams.sort,
        time: queryParams.time,
        limit: queryParams.limit
    });

    const stacksQuery = getStacksQuery(params);

    function handleRefresh() {
        $stacksQuery.refetch();
    }

    function handleFilterChange(newFilter: string) {
        queryParams.filter = newFilter;
    }
</script>

<div class="container mx-auto py-6">
    <div class="flex items-center justify-between mb-6">
        <H3>Stacks</H3>
        <RefreshButton isLoading={$stacksQuery.isPending} onClick={handleRefresh} />
    </div>

    <!-- Filter Input -->
    <div class="mb-4 flex gap-2">
        <input
            type="text"
            placeholder="Filter (e.g., status:open type:error tags:production)"
            class="flex-1 px-3 py-2 border border-gray-300 rounded"
            value={queryParams.filter}
            onchange={(e) => handleFilterChange(e.currentTarget.value)}
        />
    </div>

    <!-- Stacks List -->
    <div class="border rounded">
        {#if $stacksQuery.isPending}
            <div class="p-4 text-center text-gray-500">Loading stacks...</div>
        {:else if !$stacksQuery.data || $stacksQuery.data.length === 0}
            <div class="p-4 text-center text-gray-500">No stacks found</div>
        {:else}
            {#each $stacksQuery.data as stack (stack.id)}
                <div class="border-b p-4 hover:bg-gray-50 cursor-pointer last:border-b-0">
                    <div class="font-medium">{stack.title || 'Untitled'}</div>
                    <div class="text-sm text-gray-600 mt-1">
                        {#if stack.tags && stack.tags.length > 0}
                            <span>{stack.tags.join(', ')} · </span>
                        {/if}
                        <span>{stack.totalOccurrences || 0} events</span>
                    </div>
                    <div class="text-xs text-gray-500 mt-1">
                        {#if stack.firstOccurrence}
                            First: {new Date(stack.firstOccurrence).toLocaleDateString()} ·
                        {/if}
                        {#if stack.lastOccurrence}
                            Last: {new Date(stack.lastOccurrence).toLocaleDateString()} ·
                        {/if}
                        Status: <span class="font-medium">{stack.status}</span>
                    </div>
                </div>
            {/each}
        {/if}
    </div>

    <!-- Pagination Info -->
    {#if $stacksQuery.data && $stacksQuery.data.length > 0}
        <div class="mt-4 text-sm text-gray-600">
            Showing {$stacksQuery.data.length} stacks
        </div>
    {/if}
</div>

<style global>
    :global(.container) {
        max-width: 1400px;
    }
</style>
