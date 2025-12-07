<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import PageNumber from '$comp/page-number.svelte';

    interface Props {
        table: Table<TData>;
    }

    let { table }: Props = $props();

    const currentPage = $derived(table.getState().pagination.pageIndex + 1);
    const totalPages = $derived(Math.max(1, table.getPageCount()));
</script>

<PageNumber {currentPage} {totalPages} />
