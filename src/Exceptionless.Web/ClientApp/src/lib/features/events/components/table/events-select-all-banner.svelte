<script lang="ts">
    import Number from '#comp/formatters/number.svelte';
    import { Button } from '#comp/ui/button/index.ts';

    interface Props {
        /** Set while every event matching the filter is selected. */
        allMatchingCount?: number;
        /** True when every row of the current page is selected. */
        isPageSelected: boolean;
        onClear: () => void;
        onSelectAllMatching: () => void;
        pageCount: number;
        /** How many events match the filter, once known. The link only appears when more events match than the page shows. */
        totalMatching?: number;
    }

    let { allMatchingCount, isPageSelected, onClear, onSelectAllMatching, pageCount, totalMatching }: Props = $props();

    const canSelectAllMatching = $derived(isPageSelected && totalMatching !== undefined && totalMatching > pageCount);
</script>

{#if allMatchingCount !== undefined}
    <div class="bg-muted text-muted-foreground flex flex-wrap items-center gap-x-2 rounded-lg px-3 py-2 text-sm" data-slot="select-all-banner" role="status">
        <span>All <Number value={allMatchingCount} /> {allMatchingCount === 1 ? 'event is' : 'events are'} selected</span>
        <span aria-hidden="true">·</span>
        <Button class="h-auto p-0" onclick={onClear} variant="link">Clear selection</Button>
    </div>
{:else if canSelectAllMatching}
    <div class="bg-muted text-muted-foreground flex flex-wrap items-center gap-x-2 rounded-lg px-3 py-2 text-sm" data-slot="select-all-banner" role="status">
        <span>All <Number value={pageCount} /> {pageCount === 1 ? 'event' : 'events'} on this page {pageCount === 1 ? 'is' : 'are'} selected.</span>
        <Button class="h-auto p-0" onclick={onSelectAllMatching} variant="link">
            Select all <Number value={totalMatching!} /> events matching this filter
        </Button>
    </div>
{/if}
