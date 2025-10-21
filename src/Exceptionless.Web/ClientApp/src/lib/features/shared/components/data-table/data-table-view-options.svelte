<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { ButtonSize } from '$comp/ui/button';
    import type { Table } from '@tanstack/svelte-table';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import ViewColumn from '@lucide/svelte/icons/columns-3';

    interface Props {
        size?: ButtonSize;
        table: Table<TData>;
    }

    let { size = 'icon', table }: Props = $props();
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
            {#each table.getAllLeafColumns() as column (column.id)}
                {#if column.getCanHide()}
                    <DropdownMenu.CheckboxItem checked={column.getIsVisible()} onclick={() => column.toggleVisibility()}>
                        {column.columnDef.header}
                    </DropdownMenu.CheckboxItem>
                {/if}
            {/each}
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>
