<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import IconViewColumn from '~icons/mdi/view-column';

    interface Props {
        table: Table<TData>;
    }

    let { table }: Props = $props();
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger asChild let:builder>
        <Button builders={[builder]} class="h-8" size="sm" variant="outline">
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
