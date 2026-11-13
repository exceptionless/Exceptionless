<script lang="ts">
    import { useQueryClient } from '@tanstack/svelte-query';
    import { useEventListener } from 'runed';
    import { toast } from 'svelte-sonner';
    import { SvelteSet } from 'svelte/reactivity';

    import type { BackgroundTaskUpdated } from '#features/websockets/models.ts';

    import { organization } from '#features/organizations/context.svelte.ts';

    import { cancelTask, getTasksQuery, invalidateTaskAffectedQueries, updateTaskFromMessage } from '../api.svelte';
    import { canCancelTask, isTaskActive, selectVisibleTasks, type ViewBackgroundTask } from '../models';
    import TaskItem from './task-item.svelte';

    const queryClient = useQueryClient();
    const tasksQuery = getTasksQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const cancelTaskMutation = cancelTask();

    const dismissedIds = new SvelteSet<string>();
    const activeSeenIds = new SvelteSet<string>();
    const cancellingIds = new SvelteSet<string>();
    let now = $state(Date.now());

    const visibleTasks = $derived(selectVisibleTasks(tasksQuery.data ?? [], dismissedIds, now));

    // Finished tasks fade out of the panel on their own, so re-evaluate time while any of them is shown.
    $effect(() => {
        if (visibleTasks.length === 0) {
            return;
        }

        const timer = window.setInterval(() => (now = Date.now()), 15_000);
        return () => window.clearInterval(timer);
    });

    // A task that was seen running and is now finished changed events and stacks, whichever way the update arrived.
    $effect(() => {
        for (const task of tasksQuery.data ?? []) {
            if (isTaskActive(task.status)) {
                // A failed task can be retried with the same id; its new attempt and outcome must be visible.
                dismissedIds.delete(task.id);
                activeSeenIds.add(task.id);
                continue;
            }

            if (!activeSeenIds.delete(task.id)) {
                continue;
            }

            cancellingIds.delete(task.id);
            void invalidateTaskAffectedQueries(queryClient);
        }
    });

    useEventListener(document, 'BackgroundTaskUpdated', (event) => {
        const update = (event as CustomEvent<BackgroundTaskUpdated>).detail;
        if (update.organization_id !== organization.current) {
            return;
        }

        void updateTaskFromMessage(queryClient, update);
    });

    async function onCancel(task: ViewBackgroundTask) {
        cancellingIds.add(task.id);
        try {
            await cancelTaskMutation.mutateAsync(task.id);
        } catch {
            toast.error('Unable to cancel the task.');
        } finally {
            // The server may only acknowledge the request while a worker is running. Allow a later retry to clear
            // an abandoned task if that worker never acknowledges cancellation.
            cancellingIds.delete(task.id);
        }
    }

    function onDismiss(task: ViewBackgroundTask) {
        dismissedIds.add(task.id);
    }
</script>

{#if visibleTasks.length > 0}
    <div
        aria-label="Background tasks"
        class="fixed bottom-4 left-4 z-40 flex w-[min(22rem,calc(100vw-2rem))] flex-col gap-2 md:left-[calc(var(--sidebar-width,16rem)+1rem)]"
        role="region"
    >
        {#each visibleTasks as task (task.id)}
            <TaskItem canCancel={canCancelTask(task, now)} isCancelling={cancellingIds.has(task.id)} {onCancel} {onDismiss} {task} />
        {/each}
    </div>
{/if}
