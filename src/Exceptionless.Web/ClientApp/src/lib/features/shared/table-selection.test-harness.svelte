<script lang="ts">
    import { createTable } from '@tanstack/svelte-table';

    import { getSharedTableOptions } from './table.svelte';

    let { paginationStrategy = 'offset' }: { paginationStrategy?: 'memory' | 'offset' } = $props();
    const queryData = $state(
        Array.from(
            {
                length: 3
            },
            (_, index) => ({
                id: `row-${index + 1}`,
                name: `Row ${index + 1}`
            })
        )
    );
    const queryParameters = $state({
        limit: 1,
        page: 1,
        sort: 'name'
    });

    const table = createTable(
        getSharedTableOptions<{ id: string; name: string }>({
            columnPersistenceKey: 'table-selection-test',
            columns: [
                {
                    accessorKey: 'name',
                    id: 'name'
                }
            ],
            get paginationStrategy() {
                return paginationStrategy;
            },
            queryData,
            queryMeta: {
                links: {
                    next: {
                        page: '2',
                        rel: 'next',
                        url: '/rows?page=2'
                    }
                },
                total: 2
            },
            queryParameters
        })
    );

    const selectedCount = $derived(Object.keys(table.store.state.rowSelection).length);
</script>

<button onclick={() => table.getRowModel().rows[0]?.toggleSelected()} type="button">Select row</button>
<button onclick={() => table.setPageIndex(1)} type="button">Next page</button>
<button
    onclick={() =>
        table.setSorting([
            {
                desc: true,
                id: 'name'
            }
        ])}
    type="button">Sort descending</button
>
<button onclick={() => (queryParameters.page = 2)} type="button">Restore page from URL</button>
<button onclick={() => (queryParameters.sort = '-name')} type="button">Restore sort from URL</button>
<span aria-label="Selected rows">{selectedCount}</span>

<button onclick={() => (queryParameters.limit = 2)} type="button">Restore limit from URL</button>
<span aria-label="Visible rows">{table.options.data.map((row) => row.id).join(',')}</span>
<span aria-label="Page index">{table.options.state?.pagination?.pageIndex}</span>
