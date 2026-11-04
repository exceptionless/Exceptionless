<script lang="ts">
    import Ban from '@lucide/svelte/icons/ban';
    import CircleAlert from '@lucide/svelte/icons/circle-alert';
    import CircleCheck from '@lucide/svelte/icons/circle-check';
    import Loader from '@lucide/svelte/icons/loader';
    import X from '@lucide/svelte/icons/x';

    import Number from '#comp/formatters/number.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import { Progress } from '#comp/ui/progress/index.ts';

    import { BackgroundTaskStatus, getProgressPercent, hasCounts, isTaskActive, type ViewBackgroundTask } from '../models';

    interface Props {
        isCancelling?: boolean;
        onCancel: (task: ViewBackgroundTask) => void;
        onDismiss: (task: ViewBackgroundTask) => void;
        task: ViewBackgroundTask;
    }

    const STATUS_LABELS: Record<BackgroundTaskStatus, string> = {
        [BackgroundTaskStatus.Cancelled]: 'cancelled',
        [BackgroundTaskStatus.Completed]: 'completed',
        [BackgroundTaskStatus.Failed]: 'failed',
        [BackgroundTaskStatus.Queued]: 'waiting to start',
        [BackgroundTaskStatus.Running]: 'in progress'
    };

    let { isCancelling = false, onCancel, onDismiss, task }: Props = $props();

    const isActive = $derived(isTaskActive(task.status));
    const percent = $derived(getProgressPercent(task));
    const counts = $derived(hasCounts(task));
    // The visible counts change on every update. Screen readers only hear the task's state, which changes rarely.
    const announcement = $derived(`${task.description}: ${STATUS_LABELS[task.status]}`);
</script>

<div class="bg-popover text-popover-foreground flex flex-col gap-2 rounded-lg border p-3 shadow-md" data-slot="task" data-status={task.status}>
    <div class="flex items-start gap-2">
        <span class="mt-0.5 shrink-0" aria-hidden="true">
            {#if task.status === BackgroundTaskStatus.Completed}
                <CircleCheck class="text-primary size-4" />
            {:else if task.status === BackgroundTaskStatus.Failed}
                <CircleAlert class="text-destructive size-4" />
            {:else if task.status === BackgroundTaskStatus.Cancelled}
                <Ban class="text-muted-foreground size-4" />
            {:else}
                <Loader class="size-4 animate-spin" />
            {/if}
        </span>
        <div class="min-w-0 flex-1">
            <span class="sr-only" role="status">{announcement}</span>
            <p class="truncate text-sm font-medium" title={task.description}>{task.description}</p>
            <p class="text-muted-foreground text-xs" data-slot="task-progress">
                {#if task.status === BackgroundTaskStatus.Queued}
                    Waiting to start
                {:else if task.status === BackgroundTaskStatus.Running}
                    {#if counts}
                        <Number value={task.processed} /> of <Number value={task.total} />
                    {:else}
                        {percent}%{#if task.message}{` · ${task.message}`}{/if}
                    {/if}
                {:else if task.status === BackgroundTaskStatus.Completed}
                    {#if counts}
                        Completed · <Number value={task.succeeded} /> processed{#if task.failed > 0}, <Number value={task.failed} /> failed{/if}
                    {:else}
                        Completed{#if task.message}{` · ${task.message}`}{/if}
                    {/if}
                {:else if task.status === BackgroundTaskStatus.Cancelled}
                    {#if counts}
                        Cancelled after <Number value={task.processed} /> of <Number value={task.total} />
                    {:else}
                        Cancelled
                    {/if}
                {:else if counts}
                    Failed after <Number value={task.processed} /> of <Number value={task.total} />{#if task.message}{` · ${task.message}`}{/if}
                {:else}
                    Failed{#if task.message}{` · ${task.message}`}{/if}
                {/if}
            </p>
        </div>
        {#if isActive}
            {#if task.can_cancel}
                <Button aria-label={`Cancel ${task.description}`} disabled={isCancelling} onclick={() => onCancel(task)} size="sm" variant="outline"
                    >Cancel</Button
                >
            {/if}
        {:else}
            <Button aria-label={`Dismiss ${task.description}`} onclick={() => onDismiss(task)} size="icon-sm" variant="ghost">
                <X />
            </Button>
        {/if}
    </div>
    {#if isActive || task.status === BackgroundTaskStatus.Completed}
        <Progress aria-label={task.description} max={100} value={percent} />
    {/if}
</div>
