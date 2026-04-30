<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { RowData, StockFeatures, Table } from '@tanstack/svelte-table';

    import { Pagination, PaginationContent, PaginationFirstButton, PaginationItem, PaginationNext, PaginationPrevious } from '$comp/ui/pagination';

    interface Props {
        table: Table<StockFeatures, TData>;
    }

    let { table }: Props = $props();
    let page = $state(1);

    const pageCount = $derived(Math.max(1, table.getPageCount() || 1));

    function handlePageChange(nextPage: number) {
        page = nextPage;
        table.setPageIndex(nextPage - 1);
    }
</script>

<Pagination count={pageCount} onPageChange={handlePageChange} {page} perPage={1} siblingCount={0} class="mx-0 w-auto justify-start">
    <PaginationContent class="gap-1">
        <PaginationItem>
            <PaginationFirstButton currentPage={page} />
        </PaginationItem>
        <PaginationItem>
            <PaginationPrevious page={{ type: 'page', value: Math.max(1, page - 1) }} isActive={false} />
        </PaginationItem>
        <PaginationItem>
            <PaginationNext page={{ type: 'page', value: Math.min(pageCount, page + 1) }} isActive={false} />
        </PaginationItem>
    </PaginationContent>
</Pagination>
