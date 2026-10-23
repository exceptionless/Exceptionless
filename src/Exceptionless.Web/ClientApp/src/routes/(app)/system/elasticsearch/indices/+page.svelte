<script lang="ts">
    import { createTable } from '@tanstack/svelte-table';

    import type { TableMemoryPagingParameters } from '#features/shared/table.svelte.ts';

    import DataTableViewOptions from '#comp/data-table/data-table-view-options.svelte';
    import * as Card from '#comp/ui/card/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { Label } from '#comp/ui/label/index.ts';
    import { Skeleton } from '#comp/ui/skeleton/index.ts';
    import { Switch } from '#comp/ui/switch/index.ts';
    import { getElasticsearchQuery } from '#features/admin/api.svelte.ts';
    import IndicesDataTable from '#features/admin/components/table/indices-data-table.svelte';
    import { getTableOptions } from '#features/admin/components/table/indices-options.svelte.ts';
    import { DEFAULT_LIMIT } from '#features/shared/api/api.svelte.ts';

    const esQuery = getElasticsearchQuery();
    const data = $derived(esQuery.data);

    let searchQuery = $state('');
    let hideSystemIndices = $state(true);

    const filteredIndices = $derived.by(() => {
        let indices = data?.index_details ?? [];

        if (hideSystemIndices) {
            indices = indices.filter((index) => !index.index?.startsWith('.'));
        }

        if (searchQuery) {
            const query = searchQuery.toLowerCase();
            indices = indices.filter((index) => index.index?.toLowerCase().includes(query));
        }

        return indices;
    });

    const queryParameters: TableMemoryPagingParameters = $state({
        limit: DEFAULT_LIMIT
    });

    const table = createTable(getTableOptions(queryParameters, () => filteredIndices));
</script>

{#if esQuery.isPending}
    <Card.Root>
        <Card.Content class="pt-6">
            <Skeleton class="h-48 w-full rounded" />
        </Card.Content>
    </Card.Root>
{:else if esQuery.isError}
    <Card.Root>
        <Card.Content class="pt-6">
            <p class="text-destructive text-sm">Failed to load Elasticsearch info. Please try again.</p>
        </Card.Content>
    </Card.Root>
{:else if data}
    <IndicesDataTable bind:limit={queryParameters.limit!} isLoading={esQuery.isPending} {table}>
        {#snippet toolbarChildren()}
            <Input bind:value={searchQuery} class="flex-1" placeholder="Filter indices..." type="search" />
            <div class="flex items-center gap-2">
                <Switch id="system-indices" bind:checked={hideSystemIndices} />
                <Label class="text-muted-foreground cursor-pointer text-xs" for="system-indices">Hide system</Label>
            </div>
            <DataTableViewOptions size="icon-lg" {table} />
        {/snippet}
    </IndicesDataTable>
{/if}
