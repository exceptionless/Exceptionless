<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { ButtonSize } from '$comp/ui/button';
    import type { Table } from '@tanstack/svelte-table';
    import type { Snippet } from 'svelte';

    import DataTableViewOptions from './data-table-view-options.svelte';

    interface Props {
        actions?: Snippet;
        children: Snippet;
        size?: ButtonSize;
        table: Table<TData>;
    }

    let { actions, children, size = 'icon', table }: Props = $props();
</script>

<div class="flex flex-col gap-2">
    <div class="flex items-center gap-2">
        {#if actions}
            {@render actions()}
        {:else}
            <DataTableViewOptions {size} {table} />
        {/if}
    </div>
    <div class="flex flex-wrap items-center gap-2">
        {@render children()}
    </div>
</div>
