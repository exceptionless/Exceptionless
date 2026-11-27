<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import { type RowData, type StockFeatures, type Table } from '@tanstack/svelte-table';
    import { toast } from 'svelte-sonner';
    import { SvelteDate } from 'svelte/reactivity';

    import type { AllMatchingTarget } from '#features/shared/all-matching-selection.svelte.ts';

    import { Button } from '#comp/ui/button/index.ts';
    import * as DropdownMenu from '#comp/ui/dropdown-menu/index.ts';
    import { postBulkOperation } from '#features/tasks/api.svelte.ts';
    import {
        BulkOperationSelectionKind,
        STACKS_CHANGE_STATUS_OPERATION,
        STACKS_DELETE_OPERATION,
        STACKS_MARK_FIXED_OPERATION,
        STACKS_SNOOZE_OPERATION
    } from '#features/tasks/models.ts';
    import { getProblemMessage } from '#shared/validation.ts';

    import { deleteStack, postChangeStatus, postMarkFixed, postMarkSnoozed } from '../api.svelte';
    import { StackStatus } from '../models';
    import ConfirmMatchingStacksDialog from './dialogs/confirm-matching-stacks-dialog.svelte';
    import MarkStackDiscardedDialog from './dialogs/mark-stack-discarded-dialog.svelte';
    import MarkStackFixedInVersionDialog from './dialogs/mark-stack-fixed-in-version-dialog.svelte';
    import RemoveStackDialog from './dialogs/remove-stack-dialog.svelte';

    interface PendingConfirm {
        /** What the choice does, shown in the confirmation, e.g. the snooze duration and when it ends. */
        detail?: string;
        label: string;
        parameters: Record<string, string>;
        success: string;
        type: string;
    }

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
    let openRemoveStackDialog = $state<boolean>(false);
    let openMarkStackDiscardedDialog = $state<boolean>(false);
    let openMarkStackFixedInVersionDialog = $state<boolean>(false);
    let pendingConfirm = $state<PendingConfirm>();
    let confirmedSelectionIdentity = $state<string>();
    let openConfirmDialog = $state(false);
    // Set before the first await so a second click can't start another operation.
    let isStarting = false;

    // Navigation or a live update can change the selection while a confirmation is open. Close it instead of letting
    // the user confirm an action for something other than what they were shown.
    $effect(() => {
        if (confirmedSelectionIdentity !== selectionIdentity) {
            openRemoveStackDialog = false;
            openMarkStackDiscardedDialog = false;
            openMarkStackFixedInVersionDialog = false;
            openConfirmDialog = false;
        }
    });

    function confirmed(): void {
        confirmedSelectionIdentity = selectionIdentity;
    }

    function openDialog(dialog: 'discarded' | 'fixed' | 'remove'): void {
        confirmed();
        openMarkStackDiscardedDialog = dialog === 'discarded';
        openMarkStackFixedInVersionDialog = dialog === 'fixed';
        openRemoveStackDialog = dialog === 'remove';
    }

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

    const startBulk = postBulkOperation();

    async function markDiscarded() {
        if (allMatching) {
            await runAllMatching(
                STACKS_CHANGE_STATUS_OPERATION,
                {
                    status: 'discarded'
                },
                'Marking as discarded'
            );
            return;
        }

        await changeStatus.mutateAsync(StackStatus.Discarded);

        if (ids.length === 1) {
            toast.success('Successfully marked stack as discarded.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as discarded.`);
        }

        table.resetRowSelection();
    }

    async function markFixed(version?: string) {
        if (allMatching) {
            await runAllMatching(
                STACKS_MARK_FIXED_OPERATION,
                version
                    ? {
                          version
                      }
                    : {},
                'Marking as fixed'
            );
            return;
        }

        await updateMarkFixed.mutateAsync(version);

        if (ids.length === 1) {
            toast.success('Successfully marked stack as fixed.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as fixed.`);
        }

        table.resetRowSelection();
    }

    async function markIgnored() {
        if (allMatching) {
            requestConfirm({
                label: 'Mark Matching Stacks Ignored',
                parameters: {
                    status: 'ignored'
                },
                success: 'Marking as ignored',
                type: STACKS_CHANGE_STATUS_OPERATION
            });
            return;
        }

        try {
            await changeStatus.mutateAsync(StackStatus.Ignored);
        } catch (error) {
            toast.error(getProblemMessage(error, 'Unable to change stack status.'));
            return;
        }

        if (ids.length === 1) {
            toast.success('Successfully marked stack as ignored.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as ignored.`);
        }

        table.resetRowSelection();
    }

    async function markOpen() {
        if (allMatching) {
            requestConfirm({
                label: 'Mark Matching Stacks Open',
                parameters: {
                    status: 'open'
                },
                success: 'Marking as open',
                type: STACKS_CHANGE_STATUS_OPERATION
            });
            return;
        }

        try {
            await changeStatus.mutateAsync(StackStatus.Open);
        } catch (error) {
            toast.error(getProblemMessage(error, 'Unable to change stack status.'));
            return;
        }

        if (ids.length === 1) {
            toast.success('Successfully marked stack as open.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as open.`);
        }

        table.resetRowSelection();
    }

    async function markSnoozed(timePeriod: '6hours' | 'day' | 'month' | 'week') {
        const snoozeUntilUtc = new SvelteDate();
        let duration: string;
        switch (timePeriod) {
            case '6hours':
                snoozeUntilUtc.setHours(snoozeUntilUtc.getHours() + 6);
                duration = '6 hours';
                break;
            case 'day':
                snoozeUntilUtc.setDate(snoozeUntilUtc.getDate() + 1);
                duration = '1 day';
                break;
            case 'week':
                snoozeUntilUtc.setDate(snoozeUntilUtc.getDate() + 7);
                duration = '1 week';
                break;
            case 'month':
            default:
                snoozeUntilUtc.setMonth(snoozeUntilUtc.getMonth() + 1);
                duration = '1 month';
                break;
        }

        if (allMatching) {
            requestConfirm({
                detail: `Snooze for ${duration}, until ${snoozeUntilUtc.toLocaleString()}.`,
                label: 'Snooze Matching Stacks',
                parameters: {
                    snooze_until_utc: snoozeUntilUtc.toISOString()
                },
                success: 'Snoozing',
                type: STACKS_SNOOZE_OPERATION
            });
            return;
        }

        try {
            await updateMarkSnoozed.mutateAsync(snoozeUntilUtc);
        } catch (error) {
            toast.error(getProblemMessage(error, 'Unable to snooze selected stacks.'));
            return;
        }

        if (ids.length === 1) {
            toast.success('Successfully marked stack as snoozed.');
        } else {
            toast.success(`Successfully marked ${Intl.NumberFormat().format(ids.length)} stacks as snoozed.`);
        }

        table.resetRowSelection();
    }

    async function remove() {
        if (allMatching) {
            await runAllMatching(STACKS_DELETE_OPERATION, {}, 'Deleting');
            return;
        }

        await removeStack.mutateAsync();

        if (ids.length === 1) {
            toast.success('Successfully deleted stack.');
        } else {
            toast.success(`Successfully deleted ${Intl.NumberFormat().format(ids.length)} stacks.`);
        }

        table.resetRowSelection();
    }

    function requestConfirm(request: PendingConfirm): void {
        confirmed();
        pendingConfirm = request;
        openConfirmDialog = true;
    }

    /** Starts the action on everything matching the filter. When it can't be started the problem is shown and the selection is kept. */
    async function runAllMatching(type: string, parameters: Record<string, string>, success: string): Promise<void> {
        const target = allMatching;
        if (!target || isStarting || confirmedSelectionIdentity !== selectionIdentity) {
            return;
        }

        isStarting = true;
        try {
            try {
                await startBulk.mutateAsync({
                    organization_id: target.organizationId,
                    parameters: Object.keys(parameters).length > 0 ? parameters : undefined,
                    selection: {
                        filter: target.filter ?? '',
                        kind: BulkOperationSelectionKind.Filter,
                        offset: target.offset,
                        time: target.time
                    },
                    type
                });
            } catch (error) {
                // Keep the selection so the user can try again, for example after another delete finishes.
                toast.error(getProblemMessage(error, 'Unable to start the operation on the matching stacks.'));
                return;
            }

            toast.success(`${success} about ${Intl.NumberFormat().format(target.count)} stacks. Progress is shown at the bottom of the page.`);
            table.resetRowSelection();
            onAllMatchingStarted?.();
        } finally {
            isStarting = false;
        }
    }

    async function runPending(): Promise<void> {
        const request = pendingConfirm;
        if (request) {
            await runAllMatching(request.type, request.parameters, request.success);
        }
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
                title={selectedCount === 0 ? 'Select one or more stacks to use bulk actions' : 'Actions'}
                variant="outline"
            >
                Actions
                <ChevronDown data-icon="inline-end" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        <DropdownMenu.Group>
            <DropdownMenu.Item title="Mark stacks as open" onclick={() => markOpen()}>Mark Open</DropdownMenu.Item>
            <DropdownMenu.Item title="Mark stacks as fixed" onclick={() => openDialog('fixed')}>Mark Fixed</DropdownMenu.Item>
            <DropdownMenu.Sub>
                <DropdownMenu.SubTrigger title="Hide stacks from reports and mutes occurrence notifications">Mark Snoozed</DropdownMenu.SubTrigger>
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
                onclick={() => openDialog('discarded')}>Mark Discarded</DropdownMenu.Item
            >
            <DropdownMenu.Separator />
            <DropdownMenu.Item onclick={() => openDialog('remove')} class="text-destructive" title="Delete stacks">Delete</DropdownMenu.Item>
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>
{#if selectedCount === 0}
    <span class="sr-only" id={bulkActionsDescriptionId}>Select one or more stacks to use bulk actions.</span>
{/if}

{#if openMarkStackDiscardedDialog}
    <MarkStackDiscardedDialog bind:open={openMarkStackDiscardedDialog} discard={markDiscarded} count={selectedCount} matchingFilter={!!allMatching} />
{/if}
{#if openMarkStackFixedInVersionDialog}
    <MarkStackFixedInVersionDialog bind:open={openMarkStackFixedInVersionDialog} save={markFixed} count={selectedCount} matchingFilter={!!allMatching} />
{/if}
{#if openRemoveStackDialog}
    <RemoveStackDialog bind:open={openRemoveStackDialog} {remove} count={selectedCount} matchingFilter={!!allMatching} />
{/if}
{#if openConfirmDialog && pendingConfirm}
    <ConfirmMatchingStacksDialog
        bind:open={openConfirmDialog}
        confirm={runPending}
        count={selectedCount}
        detail={pendingConfirm.detail}
        label={pendingConfirm.label}
    />
{/if}
