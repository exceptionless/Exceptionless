<script lang="ts">
    import Ban from '@lucide/svelte/icons/ban';
    import CircleAlert from '@lucide/svelte/icons/circle-alert';
    import CircleCheck from '@lucide/svelte/icons/circle-check';
    import Loader from '@lucide/svelte/icons/loader';
    import X from '@lucide/svelte/icons/x';

    import Number from '#comp/formatters/number.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import { Progress } from '#comp/ui/progress/index.ts';

    import { BulkOperationStatus, getProgressPercent, isBulkOperationActive, type ViewBulkOperation } from '../models';

    interface Props {
        isCancelling?: boolean;
        onCancel: (operation: ViewBulkOperation) => void;
        onDismiss: (operation: ViewBulkOperation) => void;
        operation: ViewBulkOperation;
    }

    let { isCancelling = false, onCancel, onDismiss, operation }: Props = $props();

    const isActive = $derived(isBulkOperationActive(operation.status));
    const percent = $derived(getProgressPercent(operation));
</script>

<div class="bg-popover text-popover-foreground flex flex-col gap-2 rounded-lg border p-3 shadow-md" data-slot="bulk-operation" data-status={operation.status}>
    <div class="flex items-start gap-2">
        <span class="mt-0.5 shrink-0" aria-hidden="true">
            {#if operation.status === BulkOperationStatus.Completed}
                <CircleCheck class="text-primary size-4" />
            {:else if operation.status === BulkOperationStatus.Failed}
                <CircleAlert class="text-destructive size-4" />
            {:else if operation.status === BulkOperationStatus.Cancelled}
                <Ban class="text-muted-foreground size-4" />
            {:else}
                <Loader class="size-4 animate-spin" />
            {/if}
        </span>
        <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-medium" title={operation.description}>{operation.description}</p>
            <p class="text-muted-foreground text-xs" role="status">
                {#if operation.status === BulkOperationStatus.Queued}
                    Waiting to start
                {:else if operation.status === BulkOperationStatus.Running}
                    <Number value={operation.processed} /> of <Number value={operation.total} />
                {:else if operation.status === BulkOperationStatus.Completed}
                    Completed · <Number value={operation.succeeded} /> processed{#if operation.failed > 0}, <Number value={operation.failed} /> failed{/if}
                {:else if operation.status === BulkOperationStatus.Cancelled}
                    Cancelled after <Number value={operation.processed} /> of <Number value={operation.total} />
                {:else}
                    Failed after <Number value={operation.processed} /> of <Number
                        value={operation.total}
                    />{#if operation.message}{` · ${operation.message}`}{/if}
                {/if}
            </p>
        </div>
        {#if isActive}
            <Button disabled={isCancelling} onclick={() => onCancel(operation)} size="sm" variant="outline">Cancel</Button>
        {:else}
            <Button aria-label="Dismiss" onclick={() => onDismiss(operation)} size="icon-sm" variant="ghost">
                <X />
            </Button>
        {/if}
    </div>
    {#if isActive || operation.status === BulkOperationStatus.Completed}
        <Progress aria-label={operation.description} max={100} value={percent} />
    {/if}
</div>
