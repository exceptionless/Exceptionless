<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import { Pagination, PaginationContent, PaginationFirstButton, PaginationItem, PaginationNextButton, PaginationPrevButton } from '$comp/ui/pagination';

    interface Props {
        table: Table<TData>;
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
            <PaginationPrevButton />
        </PaginationItem>
        <PaginationItem>
            <PaginationNextButton />
        </PaginationItem>
    </PaginationContent>
</Pagination>
