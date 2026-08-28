<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    import type { EventSummaryModel, SummaryTemplateKeys } from '../summary/index';

    interface Props {
        autoFillColumnId?: null | string;
        bodyChildren?: Snippet;
        footerChildren?: Snippet;
        isLoading: boolean;
        limit: number;
        onAutoFillColumnResized?: (columnId: string) => void;
        rowClick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
        rowHref?: (row: EventSummaryModel<SummaryTemplateKeys>) => string;
        table: Table<StockFeatures, EventSummaryModel<SummaryTemplateKeys>>;
        toolbarChildren?: Snippet;
        wrappedColumnIds?: readonly string[];
    }

    let {
        autoFillColumnId,
        bodyChildren,
        footerChildren,
        isLoading,
        limit = $bindable(),
        onAutoFillColumnResized,
        rowClick,
        rowHref,
        table,
        toolbarChildren,
        wrappedColumnIds = []
    }: Props = $props();
</script>

<DataTable.Root>
    {#if toolbarChildren}
        <DataTable.Toolbar {table}>
            {@render toolbarChildren()}
        </DataTable.Toolbar>
    {/if}
    <DataTable.Footer {table} class="w-full" variant="floating">
        {#if footerChildren}
            {@render footerChildren()}
        {:else}
            <DataTable.Selection {table} />
            <DataTable.Pager bind:value={limit} {table} variant="floating" />
        {/if}
    </DataTable.Footer>
    <DataTable.Body {autoFillColumnId} {onAutoFillColumnResized} {rowClick} {rowHref} {table} {wrappedColumnIds}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
        {#if bodyChildren}
            {@render bodyChildren()}
        {/if}
    </DataTable.Body>
</DataTable.Root>
