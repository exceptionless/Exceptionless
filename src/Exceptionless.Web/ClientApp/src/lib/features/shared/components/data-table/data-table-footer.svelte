<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    import { type RowData, type StockFeatures, type Table } from '@tanstack/svelte-table';

    import DataTablePageCount from './data-table-page-count.svelte';
    import DataTablePagination from './data-table-pagination.svelte';
    import { findScrollableAncestor } from './data-table-scroll';
    import DataTableSelection from './data-table-selection.svelte';

    type Props = HTMLAttributes<Element> & {
        children?: Snippet;
        table: Table<StockFeatures, TData>;
        variant?: 'floating' | 'simple';
    };

    let { children, class: className, table, variant = 'simple' }: Props = $props();

    let isFloating = $state(false);
    let toolbarElement = $state<HTMLDivElement>();

    $effect(() => {
        const element = toolbarElement;
        if (variant !== 'floating' || !element || typeof window === 'undefined') {
            isFloating = false;
            return;
        }

        const scrollContainer = findScrollableAncestor(element);

        function updateFloatingState(): void {
            const styles = window.getComputedStyle(element!);
            const stickyTop = Number.parseFloat(styles.top);

            if (styles.position !== 'sticky' || !Number.isFinite(stickyTop)) {
                isFloating = false;
                return;
            }

            const scrollContainerTop = scrollContainer?.getBoundingClientRect().top ?? 0;
            const stickyBoundary = scrollContainerTop + (scrollContainer?.clientTop ?? 0) + stickyTop;
            isFloating = Math.abs(element!.getBoundingClientRect().top - stickyBoundary) < 1;
        }

        updateFloatingState();
        const scrollTarget = scrollContainer ?? window;
        scrollTarget.addEventListener('scroll', updateFloatingState, {
            passive: true
        });
        window.addEventListener('resize', updateFloatingState, {
            passive: true
        });

        return () => {
            scrollTarget.removeEventListener('scroll', updateFloatingState);
            window.removeEventListener('resize', updateFloatingState);
        };
    });
</script>

<div
    aria-label="Table controls"
    bind:this={toolbarElement}
    class={[
        'flex w-full items-center',
        variant === 'floating'
            ? 'border-border bg-background/95 sticky top-2 z-30 flex-wrap justify-between gap-0 rounded-lg border backdrop-blur-sm'
            : 'justify-end gap-2',
        isFloating && 'floating-glow',
        className
    ]}
    data-floating={isFloating ? '' : undefined}
    data-slot="data-table-footer"
    data-variant={variant}
    role="toolbar"
>
    {#if children}
        {@render children()}
    {:else}
        <DataTableSelection {table} />
        <div class="flex items-center gap-4">
            <DataTablePageCount {table} />
            <DataTablePagination {table} />
        </div>
    {/if}
</div>

<style>
    .floating-glow {
        box-shadow:
            0 0 0 1px color-mix(in oklab, var(--foreground) 32%, transparent),
            0 0 14px 2px color-mix(in oklab, var(--foreground) 18%, transparent);
    }
</style>
