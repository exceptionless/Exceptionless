<script lang="ts">
    import { onMount, type Snippet } from 'svelte';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();

    let canScrollLeft = $state(false);
    let canScrollRight = $state(false);
    const componentId = $props.id();
    let floatingScrollbarElement = $state<HTMLDivElement | null>(null);
    let hasHorizontalOverflow = $state(false);
    let horizontalScrollMaximum = $state(0);
    let horizontalScrollPosition = $state(0);
    let horizontalScrollWidth = $state(0);
    const tableScrollContainerId = `data-table-scroll-container-${componentId}`;
    let wrapperElement = $state<HTMLDivElement | null>(null);
    let tableScrollContainer: HTMLDivElement | null = null;

    function updateHorizontalOverflow(): void {
        if (!tableScrollContainer) {
            return;
        }

        horizontalScrollWidth = tableScrollContainer.scrollWidth;
        hasHorizontalOverflow = horizontalScrollWidth > tableScrollContainer.clientWidth + 1;

        const maximumScrollLeft = horizontalScrollWidth - tableScrollContainer.clientWidth;
        horizontalScrollMaximum = Math.max(0, maximumScrollLeft);
        horizontalScrollPosition = tableScrollContainer.scrollLeft;
        canScrollLeft = hasHorizontalOverflow && tableScrollContainer.scrollLeft > 1;
        canScrollRight = hasHorizontalOverflow && tableScrollContainer.scrollLeft < maximumScrollLeft - 1;

        if (floatingScrollbarElement && floatingScrollbarElement.scrollLeft !== tableScrollContainer.scrollLeft) {
            floatingScrollbarElement.scrollLeft = tableScrollContainer.scrollLeft;
        }
    }

    function onFloatingScrollbarScroll(): void {
        if (!floatingScrollbarElement || !tableScrollContainer) {
            return;
        }

        tableScrollContainer.scrollLeft = floatingScrollbarElement.scrollLeft;
        updateHorizontalOverflow();
    }

    function onFloatingScrollbarKeydown(event: KeyboardEvent): void {
        if (!floatingScrollbarElement || !tableScrollContainer) {
            return;
        }

        let nextPosition: number;
        switch (event.key) {
            case 'ArrowLeft':
                nextPosition = floatingScrollbarElement.scrollLeft - 40;
                break;
            case 'ArrowRight':
                nextPosition = floatingScrollbarElement.scrollLeft + 40;
                break;
            case 'End':
                nextPosition = horizontalScrollMaximum;
                break;
            case 'Home':
                nextPosition = 0;
                break;
            case 'PageDown':
                nextPosition = floatingScrollbarElement.scrollLeft + tableScrollContainer.clientWidth * 0.8;
                break;
            case 'PageUp':
                nextPosition = floatingScrollbarElement.scrollLeft - tableScrollContainer.clientWidth * 0.8;
                break;
            default:
                return;
        }

        event.preventDefault();
        floatingScrollbarElement.scrollLeft = Math.min(horizontalScrollMaximum, Math.max(0, nextPosition));
        onFloatingScrollbarScroll();
    }

    onMount(() => {
        tableScrollContainer = wrapperElement?.querySelector<HTMLDivElement>(':scope > [data-slot="table-container"]') ?? null;
        const tableElement = tableScrollContainer?.querySelector<HTMLTableElement>('[data-slot="table"]') ?? null;
        if (!tableScrollContainer || !tableElement) {
            return;
        }

        tableScrollContainer.id = tableScrollContainerId;
        updateHorizontalOverflow();
        tableScrollContainer.addEventListener('scroll', updateHorizontalOverflow, {
            passive: true
        });
        window.addEventListener('resize', updateHorizontalOverflow);

        const resizeObserver = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(updateHorizontalOverflow);
        resizeObserver?.observe(tableScrollContainer);
        resizeObserver?.observe(tableElement);

        return () => {
            resizeObserver?.disconnect();
            tableScrollContainer?.removeEventListener('scroll', updateHorizontalOverflow);
            window.removeEventListener('resize', updateHorizontalOverflow);
            tableScrollContainer = null;
        };
    });
</script>

<div bind:this={wrapperElement} class="data-table-scroll-container relative rounded-md border">
    {@render children()}
    {#if canScrollLeft}
        <div
            aria-hidden="true"
            class="from-background/80 pointer-events-none absolute top-0 bottom-4 left-0 z-10 w-8 bg-linear-to-r to-transparent"
            data-scroll-edge="left"
        ></div>
    {/if}
    {#if canScrollRight}
        <div
            aria-hidden="true"
            class="to-background/80 pointer-events-none absolute top-0 right-0 bottom-4 z-10 w-8 bg-linear-to-r from-transparent"
            data-scroll-edge="right"
        ></div>
    {/if}
    <div
        aria-hidden={!hasHorizontalOverflow}
        aria-label="Horizontal table scroll"
        aria-controls={tableScrollContainerId}
        aria-orientation="horizontal"
        aria-valuemax={horizontalScrollMaximum}
        aria-valuemin={0}
        aria-valuenow={horizontalScrollPosition}
        bind:this={floatingScrollbarElement}
        class={[
            'bg-background/95 sticky bottom-0 z-20 h-4 w-full overflow-x-auto overflow-y-hidden border-t backdrop-blur',
            !hasHorizontalOverflow && 'hidden'
        ]}
        onkeydown={onFloatingScrollbarKeydown}
        onscroll={onFloatingScrollbarScroll}
        role="scrollbar"
        tabindex={hasHorizontalOverflow ? 0 : -1}
    >
        <div class="h-px" style:width={`${horizontalScrollWidth}px`}></div>
    </div>
</div>

<style>
    .data-table-scroll-container :global([data-slot='table-container']) {
        scrollbar-width: none;
    }

    .data-table-scroll-container :global([data-slot='table-container']::-webkit-scrollbar) {
        display: none;
    }
</style>
