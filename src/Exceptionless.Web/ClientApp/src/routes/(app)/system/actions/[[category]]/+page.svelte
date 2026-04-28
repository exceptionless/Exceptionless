<script lang="ts">
    import type { MaintenanceAction, MaintenanceActionCategory } from '$features/admin/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import { Separator } from '$comp/ui/separator';
    import { runMaintenanceJobMutation } from '$features/admin/api.svelte';
    import RunMaintenanceJobDialog from '$features/admin/components/dialogs/run-maintenance-job-dialog.svelte';
    import { maintenanceActions } from '$features/admin/models';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Play from '@lucide/svelte/icons/play';
    import TriangleAlert from '@lucide/svelte/icons/triangle-alert';
    import { toast } from 'svelte-sonner';

    const categories: ('All' | MaintenanceActionCategory)[] = ['All', 'Billing', 'Configuration', 'Elasticsearch', 'Maintenance', 'Security', 'Users'];

    const activeCategory = $derived<'All' | MaintenanceActionCategory>((page.params.category as MaintenanceActionCategory | undefined) ?? 'All');

    let searchQuery = $state(page.url.searchParams.get('q') ?? '');
    let selectedAction = $state<MaintenanceAction | undefined>();
    let openDialog = $state(false);
    let toastId = $state<number | string>();

    const runJob = runMaintenanceJobMutation();

    $effect(() => {
        const query = searchQuery.trim();
        const currentQuery = page.url.searchParams.get('q') ?? '';
        if (query !== currentQuery) {
            const url = new URL(page.url);
            if (query) {
                url.searchParams.set('q', query);
            } else {
                url.searchParams.delete('q');
            }

            goto(url.pathname + url.search, { keepFocus: true, noScroll: true, replaceState: true });
        }
    });

    function selectCategory(category: 'All' | MaintenanceActionCategory) {
        const query = searchQuery.trim();
        const actionsBase = resolve('/(app)/system/actions');
        const base = category === 'All' ? actionsBase : `${actionsBase}/${category}`;

        if (query) {
            goto(`${base}?q=${encodeURIComponent(query)}`, { noScroll: true });
            return;
        }

        goto(base, { noScroll: true });
    }

    const filteredActions = $derived(
        maintenanceActions.filter((action) => {
            const query = searchQuery.trim().toLowerCase();
            const matchesSearch = query === '' || action.label.toLowerCase().includes(query) || action.description.toLowerCase().includes(query);
            const matchesCategory = activeCategory === 'All' || action.category === activeCategory;
            return matchesSearch && matchesCategory;
        })
    );

    const destructiveCount = $derived(filteredActions.filter((a) => a.dangerous).length);

    function handleRun(action: MaintenanceAction) {
        selectedAction = action;
        openDialog = true;
    }

    async function handleConfirm(params: Parameters<typeof runJob.mutateAsync>[0]) {
        toast.dismiss(toastId);
        try {
            await runJob.mutateAsync(params);
            toastId = toast.success(`Successfully enqueued the "${selectedAction?.label}" job.`);
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while starting the job: ${message}`);
            throw error;
        }
    }
</script>

<div class="space-y-4">
    <div>
        <H3>Actions</H3>
        <Muted>Run maintenance jobs and system operations.</Muted>
    </div>
    <Separator />

    <!-- Category nav + search -->
    <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="flex flex-wrap gap-1">
            {#each categories as category (category)}
                <Button
                    class="rounded-md px-3 py-1.5 text-sm font-medium transition-colors {activeCategory === category
                        ? 'bg-secondary text-secondary-foreground'
                        : 'text-muted-foreground hover:bg-muted hover:text-foreground'}"
                    onclick={() => selectCategory(category)}
                    size="sm"
                    variant="ghost"
                >
                    {category}
                </Button>
            {/each}
        </div>
        <Input bind:value={searchQuery} class="w-48 text-sm" placeholder="Search actions..." type="search" />
    </div>

    <!-- Action list -->
    <div class="rounded-md border">
        <div class="flex items-center justify-between border-b px-4 py-2">
            <span class="text-muted-foreground text-xs font-semibold tracking-wide uppercase">Action</span>
            <span class="text-muted-foreground text-xs">
                {filteredActions.length}
                {filteredActions.length === 1 ? 'action' : 'actions'}
                {#if destructiveCount > 0}
                    · <span class="text-destructive font-medium">{destructiveCount} destructive</span>
                {/if}
            </span>
        </div>
        {#each filteredActions as action (action.name)}
            <div
                class="flex items-start justify-between gap-4 border-b px-4 py-4 last:border-0 {action.dangerous
                    ? 'hover:bg-destructive/5'
                    : 'hover:bg-muted/30'}"
            >
                <div class="min-w-0 flex-1 space-y-1">
                    <div class="flex flex-wrap items-center gap-2">
                        <span class="font-medium">{action.label}</span>
                        {#if activeCategory === 'All'}
                            <span class="bg-muted text-muted-foreground inline-flex items-center rounded px-1.5 py-0.5 text-xs font-medium">
                                {action.category.toUpperCase()}
                            </span>
                        {/if}
                        {#if action.dangerous}
                            <span
                                class="border-destructive/50 text-destructive inline-flex items-center gap-1 rounded border px-1.5 py-0.5 text-xs font-medium"
                            >
                                <TriangleAlert class="size-3" />
                                DESTRUCTIVE
                            </span>
                        {/if}
                    </div>
                    <p class="text-muted-foreground text-sm leading-relaxed">{action.description}</p>
                </div>
                <Button class="shrink-0" onclick={() => handleRun(action)} size="sm" variant={action.dangerous ? 'destructive' : 'outline'}>
                    <Play class="size-3.5" />
                    Run
                </Button>
            </div>
        {/each}
        {#if filteredActions.length === 0}
            <div class="text-muted-foreground px-4 py-10 text-center text-sm">No actions found.</div>
        {/if}
    </div>
</div>

{#if selectedAction && openDialog}
    <RunMaintenanceJobDialog bind:open={openDialog} action={selectedAction} onConfirm={handleConfirm} />
{/if}
