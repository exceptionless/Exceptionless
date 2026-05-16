<script lang="ts">
    import DelayedRender from '$comp/delayed-render.svelte';
    import RefreshButton from '$comp/refresh-button.svelte';
    import { H3 } from '$comp/typography';
    import { getStacksQuery, type GetStacksParams } from '$features/stacks/api.svelte';
    import type { Stack } from '$features/stacks/models';
    import { organization } from '$features/organizations/context.svelte';
    import StackStatusBadge from '$features/stacks/components/stack-status-badge.svelte';
    import StacksBulkActionsButton from '$features/stacks/components/stacks-bulk-actions-button.svelte';
    import { queryParamsState } from 'kit-query-params';
    import { watch } from 'runed';

    // Configuration
    const DEFAULT_TIME_RANGE = '[now-30d TO now]';
    const DEFAULT_PARAMS = {
        filter: 'status:open',
        limit: 25,
        sort: '-last_occurrence',
        time: DEFAULT_TIME_RANGE
    };

    // Query params
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

    // Reset on org change
    watch(
        () => organization.current,
        () => {
            Object.assign(queryParams, DEFAULT_PARAMS);
            selectedIds = [];
        },
        { lazy: true }
    );

    // Query stacks
    let params: GetStacksParams = $derived({
        filter: queryParams.filter,
        sort: queryParams.sort,
        time: queryParams.time,
        limit: queryParams.limit
    });

    const stacksQuery = getStacksQuery(params);
    const stacks = $derived($stacksQuery.data || []);

    // Row selection state
    let selectedIds = $state<string[]>([]);

    // Actions
    function handleRefresh() {
        $stacksQuery.refetch();
    }

    function handleFilterChange(newFilter: string) {
        queryParams.filter = newFilter;
    }

    function toggleRowSelection(stackId: string) {
        if (selectedIds.includes(stackId)) {
            selectedIds = selectedIds.filter(id => id !== stackId);
        } else {
            selectedIds = [...selectedIds, stackId];
        }
    }

    function toggleAllSelection() {
        if (selectedIds.length === stacks.length) {
            selectedIds = [];
        } else {
            selectedIds = stacks.map(s => s.id);
        }
    }
</script>

<div class="container mx-auto py-6">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
        <H3>Stacks</H3>
        <RefreshButton isLoading={$stacksQuery.isPending} onClick={handleRefresh} />
    </div>

    <!-- Filter Input -->
    <div class="mb-4 flex gap-2">
        <input
            type="text"
            placeholder="Filter (e.g., status:open type:error tags:production)"
            class="flex-1 px-3 py-2 border border-gray-300 rounded text-sm"
            value={queryParams.filter}
            onchange={(e) => handleFilterChange(e.currentTarget.value)}
        />
        <select
            class="px-3 py-2 border border-gray-300 rounded text-sm"
            value={queryParams.limit}
            onchange={(e) => queryParams.limit = parseInt(e.currentTarget.value)}
        >
            <option value={10}>10 per page</option>
            <option value={25}>25 per page</option>
            <option value={50}>50 per page</option>
            <option value={100}>100 per page</option>
        </select>
    </div>

    <!-- Table Container -->
    <div class="border rounded bg-white overflow-hidden">
        {#if $stacksQuery.isPending}
            <DelayedRender>
                <div class="p-8 text-center text-gray-500">
                    <div class="animate-spin inline-block w-6 h-6 border-4 border-gray-300 border-t-blue-600 rounded-full mb-2"></div>
                    <p>Loading stacks...</p>
                </div>
            </DelayedRender>
        {:else if stacks.length === 0}
            <div class="p-8 text-center text-gray-500">
                <p>No stacks found matching your filter.</p>
            </div>
        {:else}
            <!-- Table Header -->
            <div class="border-b bg-gray-50">
                <div class="flex items-center p-4 gap-3 text-sm font-medium text-gray-700">
                    <input
                        type="checkbox"
                        checked={selectedIds.length === stacks.length && stacks.length > 0}
                        onchange={toggleAllSelection}
                        class="w-4 h-4 cursor-pointer"
                    />
                    <div class="flex-1 grid grid-cols-5 gap-4">
                        <div>Title</div>
                        <div>Tags</div>
                        <div class="text-right">Events</div>
                        <div>Last Occurrence</div>
                        <div>Status</div>
                    </div>
                </div>
            </div>

            <!-- Table Rows -->
            <div class="divide-y">
                {#each stacks as stack (stack.id)}
                    <div
                        class="flex items-center p-4 gap-3 hover:bg-gray-50 cursor-pointer transition-colors"
                        onclick={() => toggleRowSelection(stack.id)}
                        role="button"
                        tabindex="0"
                        onkeydown={(e) => {
                            if (e.key === 'Enter') toggleRowSelection(stack.id);
                        }}
                    >
                        <input
                            type="checkbox"
                            checked={selectedIds.includes(stack.id)}
                            class="w-4 h-4 cursor-pointer"
                            onclick={(e) => {
                                e.stopPropagation();
                                toggleRowSelection(stack.id);
                            }}
                        />
                        <div class="flex-1 grid grid-cols-5 gap-4 text-sm items-center">
                            <div class="font-medium truncate" title={stack.title}>
                                {stack.title || 'Untitled'}
                            </div>
                            <div class="text-gray-600 truncate text-xs">
                                {(stack.tags || []).join(', ') || '-'}
                            </div>
                            <div class="text-gray-600 text-right">
                                {(stack.totalOccurrences || 0).toLocaleString()}
                            </div>
                            <div class="text-gray-600">
                                {#if stack.lastOccurrence}
                                    {new Date(stack.lastOccurrence).toLocaleDateString()}
                                {:else}
                                    -
                                {/if}
                            </div>
                            <div>
                                <StackStatusBadge status={stack.status} />
                            </div>
                        </div>
                    </div>
                {/each}
            </div>
        {/if}
    </div>

    <!-- Bulk Actions -->
    {#if selectedIds.length > 0}
        <div class="mt-4 p-4 bg-blue-50 rounded border border-blue-200 flex items-center justify-between">
            <span class="text-sm font-medium text-blue-900">
                {selectedIds.length} stack{selectedIds.length === 1 ? '' : 's'} selected
            </span>
            <div class="flex gap-2">
                <StacksBulkActionsButton {selectedIds} onActionsComplete={() => (selectedIds = [])} />
                <button
                    class="px-3 py-2 text-sm border border-blue-300 rounded hover:bg-blue-100 transition-colors"
                    onclick={() => selectedIds = []}
                >
                    Clear
                </button>
            </div>
        </div>
    {/if}

    <!-- Info -->
    <div class="mt-4 text-sm text-gray-600">
        Showing {stacks.length} stack{stacks.length === 1 ? '' : 's'}
    </div>
</div>

<style lang="postcss">
    :global(.container) {
        @apply max-w-full;
    }
</style>
