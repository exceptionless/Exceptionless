<script lang="ts">
    import { createTable } from '@tanstack/svelte-table';

    import { getSharedTableOptions, removeTableData, removeTableSelection } from './table.svelte';

    let { paginationStrategy = 'offset' }: { paginationStrategy?: 'cursor' | 'offset' } = $props();

    const queryParameters = $state({
        limit: 1,
        page: 1,
        sort: 'name'
    });

    const tableOptions = getSharedTableOptions<{ id: string; name: string }>({
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
        queryData: [
            {
                id: 'row-1',
                name: 'First row'
            },
            {
                id: 'row-2',
                name: 'Second row'
            }
        ],
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
    });
    const table = createTable(tableOptions);

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
<button
    onclick={() => {
        removeTableSelection(table, 'row-1');
        removeTableData(tableOptions, (row) => row.id === 'row-1');
    }}
    type="button">Remove first row</button
>
<span aria-label="Selected rows">{selectedCount}</span>
{#each table.getRowModel().rows as row (row.id)}
    <span>{row.original.name}</span>
{/each}
