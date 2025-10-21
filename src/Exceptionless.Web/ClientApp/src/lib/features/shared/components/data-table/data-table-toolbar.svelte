<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { ButtonSize } from '$comp/ui/button';
    import type { Table } from '@tanstack/svelte-table';
    import type { Snippet } from 'svelte';

    import DataTableViewOptions from './data-table-view-options.svelte';

    interface Props {
        children?: Snippet;
        size?: ButtonSize;
        table: Table<TData>;
    }

    let { children, size = 'icon', table }: Props = $props();
</script>

<div class="flex items-center gap-2">
    {#if children}
        {@render children()}
    {:else}
        <div class="flex-1"></div>
        <DataTableViewOptions {size} {table} />
    {/if}
</div>
