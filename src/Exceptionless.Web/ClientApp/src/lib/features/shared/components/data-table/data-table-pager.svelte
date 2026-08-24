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

    let pagerElement: HTMLElement;

    const currentPage = $derived((table.options.state?.pagination?.pageIndex ?? table.store.state.pagination.pageIndex) + 1);
    const totalPages = $derived(Math.max(1, table.getPageCount() || 1));
    const canGoNext = $derived(currentPage < totalPages);
    const canGoPrevious = $derived(currentPage > 1);

    function scrollTableIntoView(): void {
        const tableElement = pagerElement.closest<HTMLElement>('[data-slot="data-table"]');
        if (!tableElement) {
            return;
        }

        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        tableElement.scrollIntoView({
            behavior: prefersReducedMotion ? 'auto' : 'smooth',
            block: 'start'
        });
    }

    function goToNextPage(): void {
        if (canGoNext) {
            table.setPageIndex(currentPage);
            scrollTableIntoView();
        }
    }

    function goToPreviousPage(): void {
        if (canGoPrevious) {
            table.setPageIndex(currentPage - 2);
            scrollTableIntoView();
        }
    }
</script>

<nav bind:this={pagerElement} aria-label="Table pagination" class="ml-auto shrink-0">
    <ButtonGroup.Root aria-label="Pagination controls">
        <DataTablePageSize bind:value joined onPageSizeChange={scrollTableIntoView} {table} />
        <ButtonGroup.Text
            aria-label={`Page ${currentPage} of ${totalPages}`}
            class="min-w-14 justify-center rounded-none border-y-0 select-none"
            title={`Page ${currentPage} of ${totalPages}`}
        >
            <Number value={currentPage} /> / <Number value={totalPages} />
        </ButtonGroup.Text>
        <Button
            aria-label="Go to previous page"
            aria-disabled={!canGoPrevious}
            class="rounded-none border-y-0 aria-disabled:pointer-events-none aria-disabled:opacity-50"
            onclick={goToPreviousPage}
            size="icon"
            title="Previous page"
            variant="outline"
        >
            <ChevronLeftIcon />
        </Button>
        <Button
            aria-label="Go to next page"
            aria-disabled={!canGoNext}
            class="rounded-l-none! rounded-r-lg! border-y-0 border-r-0 aria-disabled:pointer-events-none aria-disabled:opacity-50"
            onclick={goToNextPage}
            size="icon"
            title="Next page"
            variant="outline"
        >
            <ChevronRightIcon />
        </Button>
    </ButtonGroup.Root>
    <span class="sr-only" aria-live="polite">Page {currentPage} of {totalPages}</span>
</nav>
