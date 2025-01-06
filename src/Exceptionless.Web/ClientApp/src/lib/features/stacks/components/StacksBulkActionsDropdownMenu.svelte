<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import Button from '$comp/ui/button/button.svelte';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { type Table as SvelteTable } from '@tanstack/svelte-table';
    import { toast } from 'svelte-sonner';
    import ChevronDown from '~icons/mdi/chevron-down';

    import { deleteStack, postChangeStatus, postMarkFixed, postMarkSnoozed } from '../api.svelte';
    import { StackStatus } from '../models';
    import MarkStackDiscardedDialog from './dialogs/MarkStackDiscardedDialog.svelte';
    import MarkStackFixedInVersionDialog from './dialogs/MarkStackFixedInVersionDialog.svelte';
    import RemoveStackDialog from './dialogs/RemoveStackDialog.svelte';

    interface Props {
        table: SvelteTable<TData>;
    }

    let { table }: Props = $props();
    const ids = $derived(table.getSelectedRowModel().flatRows.map((row) => row.id));

    let openRemoveStackDialog = $state<boolean>(false);
    let openMarkStackDiscardedDialog = $state<boolean>(false);
    let openMarkStackFixedInVersionDialog = $state<boolean>(false);

    const updateMarkFixed = postMarkFixed({
        route: {
            get ids() {
                return ids;
            }
        }
    });

    const updateMarkSnoozed = postMarkSnoozed({
        route: {
            get ids() {
                return ids;
            }
        }
    });

    const changeStatus = postChangeStatus({
        route: {
            get ids() {
                return ids;
            }
        }
    });

    const removeStack = deleteStack({
        route: {
            get ids() {
                return ids;
            }
        }
    });

    async function markOpen() {
        await changeStatus.mutateAsync(StackStatus.Open);

        if (ids.length === 1) {
            toast.success('Successfully marked stack as open.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as open.`);
        }

        table.resetRowSelection();
    }

    async function markFixed(version?: string) {
        await updateMarkFixed.mutateAsync(version);

        if (ids.length === 1) {
            toast.success('Successfully marked stack as fixed.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as fixed.`);
        }

        table.resetRowSelection();
    }

    async function markSnoozed(timePeriod?: '6hours' | 'day' | 'month' | 'week') {
        let snoozeUntilUtc = new Date();
        switch (timePeriod) {
            case '6hours':
                snoozeUntilUtc.setHours(snoozeUntilUtc.getHours() + 6);
                break;
            case 'day':
                snoozeUntilUtc.setDate(snoozeUntilUtc.getDate() + 1);
                break;
            case 'week':
                snoozeUntilUtc.setDate(snoozeUntilUtc.getDate() + 7);
                break;
            case 'month':
            default:
                snoozeUntilUtc.setMonth(snoozeUntilUtc.getMonth() + 1);
                break;
        }

        await updateMarkSnoozed.mutateAsync(snoozeUntilUtc);

        if (ids.length === 1) {
            toast.success('Successfully marked stack as snoozed.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as snoozed.`);
        }
        table.resetRowSelection();
    }

    async function markIgnored() {
        await changeStatus.mutateAsync(StackStatus.Ignored);

        if (ids.length === 1) {
            toast.success('Successfully marked stack as ignored.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as ignored.`);
        }

        table.resetRowSelection();
    }

    async function markDiscarded() {
        await changeStatus.mutateAsync(StackStatus.Discarded);

        if (ids.length === 1) {
            toast.success('Successfully marked stack as discarded.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as discarded.`);
        }

        table.resetRowSelection();
    }

    async function remove() {
        await removeStack.mutateAsync();

        if (ids.length === 1) {
            toast.success('Successfully deleted stack.');
        } else {
            toast.success(`Successfully deleted ${Intl.NumberFormat().format(ids.length)} stacks.`);
        }

        table.resetRowSelection();
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
            <DropdownMenu.Separator />
            <DropdownMenu.Item title="Mark stacks as open" onclick={() => markOpen()}>Mark Open</DropdownMenu.Item>
            <DropdownMenu.Item title="Mark stacks as fixed" onclick={() => (openMarkStackFixedInVersionDialog = true)}>Mark Fixed</DropdownMenu.Item>
            <DropdownMenu.Sub>
                <DropdownMenu.SubTrigger title="Hide stacks from reports and mutes occurrence notifications" onclick={() => markSnoozed()}
                    >Mark Snoozed</DropdownMenu.SubTrigger
                >
                <DropdownMenu.SubContent>
                    <DropdownMenu.Item onclick={() => markSnoozed('6hours')}>6 Hours</DropdownMenu.Item>
                    <DropdownMenu.Item onclick={() => markSnoozed('day')}>1 Day</DropdownMenu.Item>
                    <DropdownMenu.Item onclick={() => markSnoozed('week')}>1 Week</DropdownMenu.Item>
                    <DropdownMenu.Item onclick={() => markSnoozed('month')}>1 Month</DropdownMenu.Item>
                </DropdownMenu.SubContent>
            </DropdownMenu.Sub>
            <DropdownMenu.Item title="Stop sending occurrence notifications for these stacks" onclick={() => markIgnored()}>Mark Ignored</DropdownMenu.Item>
            <DropdownMenu.Item
                title="All future occurrences will be discarded and will not count against your event limit"
                onclick={() => (openMarkStackDiscardedDialog = true)}>Mark Discarded</DropdownMenu.Item
            >
            <DropdownMenu.Separator />
            <DropdownMenu.Item onclick={() => (openRemoveStackDialog = true)} class="text-destructive" title="Delete stacks">Delete</DropdownMenu.Item>
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if openMarkStackDiscardedDialog}
    <MarkStackDiscardedDialog bind:open={openMarkStackDiscardedDialog} discard={markDiscarded} count={ids.length} />
{/if}
{#if openMarkStackFixedInVersionDialog}
    <MarkStackFixedInVersionDialog bind:open={openMarkStackFixedInVersionDialog} save={markFixed} count={ids.length} />
{/if}
{#if openRemoveStackDialog}
    <RemoveStackDialog bind:open={openRemoveStackDialog} {remove} count={ids.length} />
{/if}
