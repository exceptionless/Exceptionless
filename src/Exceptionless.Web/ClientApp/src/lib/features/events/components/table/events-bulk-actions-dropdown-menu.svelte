<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteEvent } from '$features/events/api.svelte';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import { type RowData, type StockFeatures, type Table } from '@tanstack/svelte-table';
    import { toast } from 'svelte-sonner';

    import RemoveEventDialog from '../dialogs/remove-event-dialog.svelte';

    interface Props {
        table: Table<StockFeatures, TData>;
    }

    let { table }: Props = $props();
    const componentId = $props.id();
    const bulkActionsDescriptionId = `${componentId}-bulk-actions-description`;
    const ids = $derived(table.getSelectedRowModel().flatRows.map((row) => row.id));

    let open = $state(false);
    let openRemoveEventDialog = $state<boolean>(false);

    const removeEvents = deleteEvent({
        route: {
            get ids() {
                return ids;
            }
        }
    });

    async function remove() {
        const deletedCount = ids.length;
        await removeEvents.mutateAsync();

        if (deletedCount === 1) {
            toast.success('Successfully deleted event.');
        } else {
            toast.success(`Successfully deleted ${Intl.NumberFormat().format(deletedCount)} events.`);
        }

        table.resetRowSelection();
    }

    function setOpen(isOpen: boolean): void {
        open = ids.length > 0 && isOpen;
    }
</script>

<DropdownMenu.Root bind:open={() => open, setOpen}>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button
                {...props}
                aria-describedby={ids.length === 0 ? bulkActionsDescriptionId : undefined}
                aria-disabled={ids.length === 0}
                class="aria-disabled:cursor-not-allowed aria-disabled:opacity-50"
                title={ids.length === 0 ? 'Select one or more events to use bulk actions' : 'Bulk Actions'}
                variant="outline"
            >
                Bulk Actions
                <ChevronDown data-icon="inline-end" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        <DropdownMenu.Group>
            <DropdownMenu.Item onclick={() => (openRemoveEventDialog = true)} class="text-destructive" title="Delete event">Delete</DropdownMenu.Item>
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>
{#if ids.length === 0}
    <span class="sr-only" id={bulkActionsDescriptionId}>Select one or more events to use bulk actions.</span>
{/if}

{#if openRemoveEventDialog}
    <RemoveEventDialog bind:open={openRemoveEventDialog} {remove} count={ids.length} />
{/if}
