<script lang="ts">
    import type { MaintenanceAction, MaintenanceActionCategory } from '$features/admin/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { CodeBlock, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { getPredefinedSavedViewsMutation, runMaintenanceJobMutation } from '$features/admin/api.svelte';
    import RunMaintenanceJobDialog from '$features/admin/components/dialogs/run-maintenance-job-dialog.svelte';
    import { maintenanceActions } from '$features/admin/models';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import FileJson from '@lucide/svelte/icons/file-json';
    import Play from '@lucide/svelte/icons/play';
    import TriangleAlert from '@lucide/svelte/icons/triangle-alert';
    import { toast } from 'svelte-sonner';

    const categories: ('All' | MaintenanceActionCategory)[] = ['All', 'Billing', 'Configuration', 'Elasticsearch', 'Maintenance', 'Security', 'Users'];

    const activeCategory = $derived<'All' | MaintenanceActionCategory>((page.params.category as MaintenanceActionCategory | undefined) ?? 'All');

    let searchQuery = $state(page.url.searchParams.get('q') ?? '');
    let selectedAction = $state<MaintenanceAction | undefined>();
    let jsonOutputAction = $state<MaintenanceAction | undefined>();
    let jsonOutput = $state('');
    let runningActionName = $state<string>();
    let openDialog = $state(false);
    let openJsonDialog = $state(false);
    let toastId = $state<number | string>();

    const runJob = runMaintenanceJobMutation();
    const predefinedSavedViews = getPredefinedSavedViewsMutation();

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

    async function handleRun(action: MaintenanceAction) {
        if (action.kind === 'predefined-saved-views') {
            await showPredefinedSavedViews(action);
            return;
        }

        selectedAction = action;
        openDialog = true;
    }

    async function showPredefinedSavedViews(action: MaintenanceAction) {
        toast.dismiss(toastId);
        runningActionName = action.name;

        try {
            jsonOutput = await predefinedSavedViews.mutateAsync();
            jsonOutputAction = action;
            openJsonDialog = true;
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while loading predefined saved views: ${message}`);
        } finally {
            runningActionName = undefined;
        }
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
    <Muted>Run maintenance jobs and system operations</Muted>

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
                <Button
                    class="shrink-0"
                    disabled={action.kind === 'predefined-saved-views' && predefinedSavedViews.isPending}
                    onclick={() => {
                        void handleRun(action);
                    }}
                    size="sm"
                    variant={action.dangerous ? 'destructive' : 'outline'}
                >
                    {#if action.kind === 'predefined-saved-views'}
                        {#if predefinedSavedViews.isPending && runningActionName === action.name}
                            <Spinner />
                            Loading...
                        {:else}
                            <FileJson class="size-3.5" aria-hidden="true" />
                            View
                        {/if}
                    {:else}
                        <Play class="size-3.5" aria-hidden="true" />
                        Run
                    {/if}
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

{#if jsonOutputAction && openJsonDialog}
    <Dialog.Root bind:open={openJsonDialog}>
        <Dialog.Content class="max-h-[90vh] gap-3 sm:max-w-4xl">
            <Dialog.Header>
                <Dialog.Title>{jsonOutputAction.label}</Dialog.Title>
                <Dialog.Description>Current response from /api/v2/saved-views/predefined.</Dialog.Description>
            </Dialog.Header>
            <div class="flex justify-end">
                <CopyToClipboardButton value={jsonOutput} size="sm" variant="outline">Copy JSON</CopyToClipboardButton>
            </div>
            <CodeBlock class="max-h-[60vh] overflow-auto" code={jsonOutput} language="json" />
            <Dialog.Footer>
                <Button
                    variant="outline"
                    onclick={() => {
                        openJsonDialog = false;
                    }}
                >
                    Close
                </Button>
            </Dialog.Footer>
        </Dialog.Content>
    </Dialog.Root>
{/if}
