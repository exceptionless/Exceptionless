<script lang="ts">
    import { useQueryClient } from '@tanstack/svelte-query';
    import { useEventListener } from 'runed';
    import { toast } from 'svelte-sonner';
    import { SvelteSet } from 'svelte/reactivity';

    import type { BulkOperationUpdated } from '#features/websockets/models.ts';

    import { organization } from '#features/organizations/context.svelte.ts';

    import { cancelBulkOperation, getBulkOperationsQuery, invalidateBulkOperationAffectedQueries, updateBulkOperationFromMessage } from '../api.svelte';
    import { isBulkOperationActive, selectVisibleOperations, type ViewBulkOperation } from '../models';
    import BulkOperationItem from './bulk-operation-item.svelte';

    const queryClient = useQueryClient();
    const operationsQuery = getBulkOperationsQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const cancelOperation = cancelBulkOperation();

    const dismissedIds = new SvelteSet<string>();
    const activeSeenIds = new SvelteSet<string>();
    const cancellingIds = new SvelteSet<string>();
    let now = $state(Date.now());

    const visibleOperations = $derived(selectVisibleOperations(operationsQuery.data ?? [], dismissedIds, now));

    // Finished operations fade out of the panel on their own, so re-evaluate time while any of them is shown.
    $effect(() => {
        if (visibleOperations.length === 0) {
            return;
        }

        const timer = window.setInterval(() => (now = Date.now()), 15_000);
        return () => window.clearInterval(timer);
    });

    // An operation that was seen running and is now finished changed events and stacks, whichever way the update arrived.
    $effect(() => {
        for (const operation of operationsQuery.data ?? []) {
            if (isBulkOperationActive(operation.status)) {
                activeSeenIds.add(operation.id);
                continue;
            }

            if (!activeSeenIds.delete(operation.id)) {
                continue;
            }

            cancellingIds.delete(operation.id);
            void invalidateBulkOperationAffectedQueries(queryClient);
        }
    });

    useEventListener(document, 'BulkOperationUpdated', (event) => {
        const update = (event as CustomEvent<BulkOperationUpdated>).detail;
        if (update.organization_id !== organization.current) {
            return;
        }

        updateBulkOperationFromMessage(queryClient, update);
    });

    async function onCancel(operation: ViewBulkOperation) {
        cancellingIds.add(operation.id);
        try {
            await cancelOperation.mutateAsync(operation.id);
        } catch {
            cancellingIds.delete(operation.id);
            toast.error('Unable to cancel the operation.');
        }
    }

    function onDismiss(operation: ViewBulkOperation) {
        dismissedIds.add(operation.id);
    }
</script>

{#if visibleOperations.length > 0}
    <div
        aria-label="Bulk operations"
        class="fixed bottom-4 left-4 z-40 flex w-[min(22rem,calc(100vw-2rem))] flex-col gap-2 md:left-[calc(var(--sidebar-width,16rem)+1rem)]"
        role="region"
    >
        {#each visibleOperations as operation (operation.id)}
            <BulkOperationItem isCancelling={cancellingIds.has(operation.id)} {onCancel} {onDismiss} {operation} />
        {/each}
    </div>
{/if}
