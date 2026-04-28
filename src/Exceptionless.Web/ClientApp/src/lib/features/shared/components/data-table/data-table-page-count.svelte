<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { RowData, StockFeatures, Table } from '@tanstack/svelte-table';

    import PageNumber from '$comp/page-number.svelte';

    interface Props {
        table: Table<StockFeatures, TData>;
    }

    let { table }: Props = $props();

    const currentPage = $derived(table.store.state.pagination.pageIndex + 1);
    const totalPages = $derived(Math.max(1, table.getPageCount()));
</script>

<PageNumber {currentPage} {totalPages} />
