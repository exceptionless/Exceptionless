<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { ButtonSize } from '$comp/ui/button';
    import type { RowData, StockFeatures, Table } from '@tanstack/svelte-table';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import ViewColumn from '@lucide/svelte/icons/columns-3';

    interface Props {
        size?: ButtonSize;
        table: Table<StockFeatures, TData>;
    }

    let { size = 'icon', table }: Props = $props();

    const hideableColumns = $derived(table.getAllLeafColumns().filter((column) => column.getCanHide()));
    const visibleHideableColumnCount = $derived(hideableColumns.filter((column) => column.getIsVisible()).length);

    function canToggleColumn(column: (typeof hideableColumns)[number]): boolean {
        return !column.getIsVisible() || visibleHideableColumnCount > 1;
    }

    function toggleColumn(column: (typeof hideableColumns)[number]): void {
        if (canToggleColumn(column)) {
            column.toggleVisibility();
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} {size} variant="outline" title="Toggle columns">
                <ViewColumn class="size-4" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        <DropdownMenu.Group>
            <DropdownMenu.Label>Toggle columns</DropdownMenu.Label>
            <DropdownMenu.Separator />
            {#each hideableColumns as column (column.id)}
                <DropdownMenu.CheckboxItem checked={column.getIsVisible()} disabled={!canToggleColumn(column)} onclick={() => toggleColumn(column)}>
                    {column.columnDef.header}
                </DropdownMenu.CheckboxItem>
            {/each}
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>
