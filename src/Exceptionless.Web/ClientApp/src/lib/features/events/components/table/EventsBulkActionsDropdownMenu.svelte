<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import Button from '$comp/ui/button/button.svelte';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteEvent } from '$features/events/api.svelte';
    import { type Table as SvelteTable } from '@tanstack/svelte-table';
    import { toast } from 'svelte-sonner';
    import ChevronDown from '~icons/mdi/chevron-down';

    import RemoveEventDialog from '../dialogs/RemoveEventDialog.svelte';

    interface Props {
        table: SvelteTable<TData>;
    }

    let { table }: Props = $props();
    const ids = $derived(table.getSelectedRowModel().flatRows.map((row) => row.id));

    let openRemoveEventDialog = $state<boolean>(false);

    const removeEvents = deleteEvent({
        route: {
            get ids() {
                return ids;
            }
        }
    });

    async function remove() {
        await removeEvents.mutateAsync();
        if (ids.length === 1) {
            toast.success('Successfully deleted event.');
        } else {
            toast.success(`Successfully deleted ${Intl.NumberFormat().format(ids.length)} events.`);
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        <Button variant="outline">
            Bulk Actions
            <ChevronDown class="size-4" />
        </Button>
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        <DropdownMenu.Group>
            <DropdownMenu.GroupHeading>Bulk Actions</DropdownMenu.GroupHeading>
            <DropdownMenu.Item onclick={() => (openRemoveEventDialog = true)} class="text-destructive" title="Delete event">Delete</DropdownMenu.Item>
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if openRemoveEventDialog}
    <RemoveEventDialog bind:open={openRemoveEventDialog} {remove} count={ids.length} />
{/if}
