<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { RowData, StockFeatures, Table } from '@tanstack/svelte-table';

    import { Button } from '$comp/ui/button';
    import ChevronLeftIcon from '@lucide/svelte/icons/chevron-left';
    import ChevronRightIcon from '@lucide/svelte/icons/chevron-right';
    import ChevronsLeftIcon from '@lucide/svelte/icons/chevrons-left';

    interface Props {
        table: Table<StockFeatures, TData>;
    }

    let { table }: Props = $props();
    const page = $derived((table.options.state?.pagination?.pageIndex ?? table.store.state.pagination.pageIndex) + 1);
    const pageCount = $derived(Math.max(1, table.getPageCount() || 1));
    const canGoNext = $derived(page < pageCount);
    const canGoPrevious = $derived(page > 1);
    const showFirst = $derived(page >= 3);

    function goToFirstPage(): void {
        table.setPageIndex(0);
    }

    function goToNextPage(): void {
        if (canGoNext) {
            table.setPageIndex(page);
        }
    }

    function goToPreviousPage(): void {
        if (canGoPrevious) {
            table.setPageIndex(page - 2);
        }
    }
</script>

<nav aria-label="pagination" class="mx-0 flex w-auto justify-start">
    <ul class="flex flex-row items-center gap-1">
        <li>
            <Button
                aria-hidden={showFirst ? undefined : true}
                aria-label="Go to first page"
                class={showFirst ? undefined : 'pointer-events-none invisible'}
                disabled={!showFirst}
                onclick={goToFirstPage}
                title="First page"
                variant="ghost"
            >
                <ChevronsLeftIcon class="size-4" />
                <span class="sr-only">Go to first page</span>
            </Button>
        </li>
        <li>
            <Button aria-label="Go to previous page" disabled={!canGoPrevious} onclick={goToPreviousPage} title="Previous page" variant="ghost">
                <ChevronLeftIcon data-icon="inline-start" />
                <span class="cn-pagination-previous-text hidden sm:block">Previous</span>
            </Button>
        </li>
        <li>
            <Button aria-label="Go to next page" disabled={!canGoNext} onclick={goToNextPage} title="Next page" variant="ghost">
                <span class="cn-pagination-next-text hidden sm:block">Next</span>
                <ChevronRightIcon data-icon="inline-end" />
            </Button>
        </li>
    </ul>
</nav>
