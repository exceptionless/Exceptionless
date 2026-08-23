<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { RowData, StockFeatures, Table } from '@tanstack/svelte-table';

    import Number from '$comp/formatters/number.svelte';
    import { Button } from '$comp/ui/button';
    import * as ButtonGroup from '$comp/ui/button-group';
    import ChevronLeftIcon from '@lucide/svelte/icons/chevron-left';
    import ChevronRightIcon from '@lucide/svelte/icons/chevron-right';

    import DataTablePageSize from './data-table-page-size.svelte';

    interface Props {
        table: Table<StockFeatures, TData>;
        value: number;
    }

    let { table, value = $bindable() }: Props = $props();

    const currentPage = $derived((table.options.state?.pagination?.pageIndex ?? table.store.state.pagination.pageIndex) + 1);
    const totalPages = $derived(Math.max(1, table.getPageCount() || 1));
    const canGoNext = $derived(currentPage < totalPages);
    const canGoPrevious = $derived(currentPage > 1);

    function goToNextPage(): void {
        if (canGoNext) {
            table.setPageIndex(currentPage);
        }
    }

    function goToPreviousPage(): void {
        if (canGoPrevious) {
            table.setPageIndex(currentPage - 2);
        }
    }
</script>

<ButtonGroup.Root aria-label="Table pagination" class="ml-auto">
    <DataTablePageSize bind:value {table} />
    <ButtonGroup.Text aria-label={`Page ${currentPage} of ${totalPages}`} class="min-w-14 justify-center" title={`Page ${currentPage} of ${totalPages}`}>
        <Number value={currentPage} /> / <Number value={totalPages} />
    </ButtonGroup.Text>
    <Button aria-label="Go to previous page" disabled={!canGoPrevious} onclick={goToPreviousPage} size="icon-sm" title="Previous page" variant="outline">
        <ChevronLeftIcon />
    </Button>
    <Button aria-label="Go to next page" disabled={!canGoNext} onclick={goToNextPage} size="icon-sm" title="Next page" variant="outline">
        <ChevronRightIcon />
    </Button>
</ButtonGroup.Root>
