<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import { type RowData, type StockFeatures, type Table } from '@tanstack/svelte-table';
    import { toast } from 'svelte-sonner';

    import { Button } from '#comp/ui/button/index.ts';
    import * as DropdownMenu from '#comp/ui/dropdown-menu/index.ts';
    import { deleteEvent } from '#features/events/api.svelte.ts';
    import { postBulkOperation } from '#features/tasks/api.svelte.ts';
    import { BulkOperationSelectionKind, EVENTS_DELETE_OPERATION } from '#features/tasks/models.ts';

    import type { AllMatchingTarget } from './all-matching-selection.svelte';

    import RemoveEventDialog from '../dialogs/remove-event-dialog.svelte';

    interface Props {
        /** When set, actions run against the filter instead of the selected rows. */
        allMatching?: AllMatchingTarget;
        /** Called once an all-matching action was started, so the selection can be cleared. */
        onAllMatchingStarted?: () => void;
        table: Table<StockFeatures, TData>;
    }

    let { allMatching, onAllMatchingStarted, table }: Props = $props();
    const componentId = $props.id();
    const bulkActionsDescriptionId = `${componentId}-bulk-actions-description`;
    const ids = $derived(table.getSelectedRowModel().flatRows.map((row) => row.id));
    const selectedCount = $derived(allMatching ? allMatching.count : ids.length);
    const selectionIdentity = $derived(
        JSON.stringify(
            allMatching
                ? ['filter', allMatching.organizationId, allMatching.filter ?? '', allMatching.time ?? null, allMatching.offset ?? null]
                : ['ids', ...ids.toSorted()]
        )
    );

    let open = $state(false);
    let openRemoveEventDialog = $state<boolean>(false);
    let confirmedSelectionIdentity = $state<string>();

    $effect(() => {
        if (openRemoveEventDialog && confirmedSelectionIdentity !== selectionIdentity) {
            openRemoveEventDialog = false;
        }
    });

    const removeEvents = deleteEvent();

    const removeMatchingEvents = postBulkOperation();

    function confirmRemove(): void {
        confirmedSelectionIdentity = selectionIdentity;
        openRemoveEventDialog = true;
    }

    async function remove() {
        // Navigation or a live update can change the selection while the confirmation is open.
        if (confirmedSelectionIdentity !== selectionIdentity) {
            return;
        }

        if (allMatching) {
            await removeAllMatching(allMatching);
            return;
        }

        const deletedCount = ids.length;
        await removeEvents.mutateAsync([...ids]);

        if (deletedCount === 1) {
            toast.success('Successfully deleted event.');
        } else {
            toast.success(`Successfully deleted ${Intl.NumberFormat().format(deletedCount)} events.`);
        }

        table.resetRowSelection();
    }

    async function removeAllMatching(target: AllMatchingTarget) {
        try {
            await removeMatchingEvents.mutateAsync({
                organization_id: target.organizationId,
                selection: {
                    filter: target.filter ?? '',
                    kind: BulkOperationSelectionKind.Filter,
                    offset: target.offset,
                    time: target.time
                },
                type: EVENTS_DELETE_OPERATION
            });
        } catch (error) {
            // Keep the selection so the user can try again, for example after another delete finishes.
            toast.error((error as Partial<ProblemDetails>)?.title || 'Unable to start deleting the matching events.');
            return;
        }

        toast.success(`Deleting ${Intl.NumberFormat().format(target.count)} events. Progress is shown at the bottom of the page.`);
        table.resetRowSelection();
        onAllMatchingStarted?.();
    }

    function setOpen(isOpen: boolean): void {
        open = selectedCount > 0 && isOpen;
    }
</script>

<DropdownMenu.Root bind:open={() => open, setOpen}>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button
                {...props}
                aria-describedby={selectedCount === 0 ? bulkActionsDescriptionId : undefined}
                aria-disabled={selectedCount === 0}
                class="rounded-l-lg rounded-r-none border-0 border-r aria-disabled:cursor-not-allowed aria-disabled:opacity-50"
                title={selectedCount === 0 ? 'Select one or more events to use bulk actions' : 'Actions'}
                variant="outline"
            >
                Actions
                <ChevronDown data-icon="inline-end" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        <DropdownMenu.Group>
            <DropdownMenu.Item onclick={confirmRemove} class="text-destructive" title="Delete event">Delete</DropdownMenu.Item>
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>
{#if selectedCount === 0}
    <span class="sr-only" id={bulkActionsDescriptionId}>Select one or more events to use bulk actions.</span>
{/if}

{#if openRemoveEventDialog}
    <RemoveEventDialog bind:open={openRemoveEventDialog} {remove} count={selectedCount} matchingFilter={!!allMatching} />
{/if}
