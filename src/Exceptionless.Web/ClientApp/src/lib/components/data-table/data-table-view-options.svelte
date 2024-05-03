<script lang="ts">
    import IconViewColumn from '~icons/mdi/view-column';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';

    import type { Table } from '@tanstack/svelte-table';

    type TData = $$Generic;
    export let table: Table<TData>;
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger asChild let:builder>
        <Button variant="outline" size="sm" class="h-8" builders={[builder]}>
            <IconViewColumn class="mr-2 h-4 w-4" />
            View
        </Button>
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        <DropdownMenu.Label>Toggle columns</DropdownMenu.Label>
        <DropdownMenu.Separator />
        {#each table.getAllLeafColumns() as column (column.id)}
            {#if column.getCanHide()}
                <DropdownMenu.CheckboxItem checked={column.getIsVisible()} on:click={() => column.toggleVisibility()}>
                    {column.columnDef.header}
                </DropdownMenu.CheckboxItem>
            {/if}
        {/each}
    </DropdownMenu.Content>
</DropdownMenu.Root>
