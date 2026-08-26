<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { RowData, StockFeatures, Table } from '@tanstack/svelte-table';

    import Number from '$comp/formatters/number.svelte';
    import { Button } from '$comp/ui/button';
    import * as ButtonGroup from '$comp/ui/button-group';
    import { cn } from '$lib/utils';
    import ChevronLeftIcon from '@lucide/svelte/icons/chevron-left';
    import ChevronRightIcon from '@lucide/svelte/icons/chevron-right';
    import ChevronsLeftIcon from '@lucide/svelte/icons/chevrons-left';

    import DataTablePageSize from './data-table-page-size.svelte';
    import { scrollTableToFirstRow } from './data-table-scroll';

    interface Props {
        table: Table<StockFeatures, TData>;
        value: number;
        variant?: 'floating' | 'simple';
    }

    let { table, value = $bindable(), variant = 'simple' }: Props = $props();
    let pagerElement = $state<HTMLElement>();

    const currentPage = $derived((table.options.state?.pagination?.pageIndex ?? table.store.state.pagination.pageIndex) + 1);
    const totalPages = $derived(Math.max(1, table.getPageCount() || 1));
    const canGoNext = $derived(currentPage < totalPages);
    const canGoPrevious = $derived(currentPage > 1);

    function goToFirstPage(event: MouseEvent): void {
        if (canGoPrevious) {
            goToPage(0, event);
        }
    }

    function goToNextPage(event: MouseEvent): void {
        if (canGoNext) {
            goToPage(currentPage, event);
        }
    }

    function goToPreviousPage(event: MouseEvent): void {
        if (canGoPrevious) {
            goToPage(currentPage - 2, event);
        }
    }

    function goToPage(pageIndex: number, event: MouseEvent): void {
        const trigger = event.currentTarget as HTMLElement;
        if (shouldAdjustScroll(trigger)) {
            scrollTableToFirstRow(trigger);
        }

        table.setPageIndex(pageIndex);
    }

    function onBeforePageSizeChange(): void {
        if (pagerElement && shouldAdjustScroll(pagerElement)) {
            scrollTableToFirstRow(pagerElement);
        }
    }

    function shouldAdjustScroll(trigger: HTMLElement): boolean {
        return variant === 'floating' && trigger.closest('[data-slot="data-table-footer"]')?.hasAttribute('data-floating') === true;
    }
</script>

<nav aria-label="Table pagination" bind:this={pagerElement} class="ml-auto shrink-0" data-variant={variant}>
    <ButtonGroup.Root aria-label="Pagination controls">
        <DataTablePageSize bind:value joined={variant === 'floating'} {onBeforePageSizeChange} size="default" {table} />
        <ButtonGroup.Text
            aria-label={`Page ${currentPage} of ${totalPages}`}
            class={cn('min-w-14 justify-center select-none', variant === 'floating' && 'rounded-none border-y-0')}
            title={`Page ${currentPage} of ${totalPages}`}
        >
            <Number value={currentPage} /> / <Number value={totalPages} />
        </ButtonGroup.Text>
        <Button
            aria-label="Go to first page"
            aria-disabled={!canGoPrevious}
            class={cn('aria-disabled:pointer-events-none aria-disabled:opacity-50', variant === 'floating' && 'rounded-none border-y-0')}
            onclick={goToFirstPage}
            size="icon"
            title="First page"
            variant="outline"
        >
            <ChevronsLeftIcon />
        </Button>
        <Button
            aria-label="Go to previous page"
            aria-disabled={!canGoPrevious}
            class={cn('aria-disabled:pointer-events-none aria-disabled:opacity-50', variant === 'floating' && 'rounded-none border-y-0')}
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
            class={cn(
                'aria-disabled:pointer-events-none aria-disabled:opacity-50',
                variant === 'floating' && 'rounded-l-none! rounded-r-lg! border-y-0 border-r-0'
            )}
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
