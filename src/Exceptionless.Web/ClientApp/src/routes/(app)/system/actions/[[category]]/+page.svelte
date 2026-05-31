<script lang="ts">
    import type { MaintenanceAction, MaintenanceActionCategory } from '$features/admin/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import * as Select from '$comp/ui/select';
    import { Spinner } from '$comp/ui/spinner';
    import * as Tabs from '$comp/ui/tabs';
    import { Textarea } from '$comp/ui/textarea';
    import { getOrgSavedViewsExportMutation, getPredefinedSavedViewsMutation, putPredefinedSavedViewsMutation, runMaintenanceJobMutation } from '$features/admin/api.svelte';
    import RunMaintenanceJobDialog from '$features/admin/components/dialogs/run-maintenance-job-dialog.svelte';
    import { maintenanceActions } from '$features/admin/models';
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import FileJson from '@lucide/svelte/icons/file-json';
    import Play from '@lucide/svelte/icons/play';
    import TriangleAlert from '@lucide/svelte/icons/triangle-alert';
    import { toast } from 'svelte-sonner';

    const categories: ('All' | MaintenanceActionCategory)[] = ['All', 'Billing', 'Configuration', 'Elasticsearch', 'Maintenance', 'Security', 'Users'];

    const activeCategory = $derived<'All' | MaintenanceActionCategory>((page.params.category as MaintenanceActionCategory | undefined) ?? 'All');

    let searchQuery = $state(page.url.searchParams.get('q') ?? '');
    let selectedAction = $state<MaintenanceAction | undefined>();
    let runningActionName = $state<string>();
    let openDialog = $state(false);
    let openPredefinedDialog = $state(false);
    let toastId = $state<number | string>();

    // Predefined saved views dialog state
    let predefinedJson = $state('');
    let predefinedTab = $state('config');
    let selectedOrgId = $state(organization.current ?? '');

    const runJob = runMaintenanceJobMutation();
    const predefinedSavedViews = getPredefinedSavedViewsMutation();
    const orgExport = getOrgSavedViewsExportMutation();
    const savePredefined = putPredefinedSavedViewsMutation();
    const orgsQuery = getOrganizationsQuery({ params: { mode: null } });

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
            predefinedJson = await predefinedSavedViews.mutateAsync();
            predefinedTab = 'config';
            openPredefinedDialog = true;
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while loading predefined saved views: ${message}`);
        } finally {
            runningActionName = undefined;
        }
    }

    async function handleLoadOrgViews() {
        if (!selectedOrgId) return;

        try {
            predefinedJson = await orgExport.mutateAsync(selectedOrgId);
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while exporting org views: ${message}`);
        }
    }

    async function handleSavePredefined() {
        try {
            predefinedJson = await savePredefined.mutateAsync(predefinedJson);
            toastId = toast.success('Predefined saved views updated successfully.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while saving predefined saved views: ${message}`);
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

{#if openPredefinedDialog}
    <Dialog.Root bind:open={openPredefinedDialog}>
        <Dialog.Content class="max-h-[90vh] gap-3 sm:max-w-4xl">
            <Dialog.Header>
                <Dialog.Title>Predefined Saved Views</Dialog.Title>
                <Dialog.Description>View, edit, or replace the predefined saved views configuration.</Dialog.Description>
            </Dialog.Header>
            <Tabs.Root bind:value={predefinedTab}>
                <Tabs.List>
                    <Tabs.Trigger value="config">Current Config</Tabs.Trigger>
                    <Tabs.Trigger value="org">Load from Org</Tabs.Trigger>
                </Tabs.List>
                <Tabs.Content value="config">
                    <Muted class="mb-2">The current predefined saved views served by the API.</Muted>
                </Tabs.Content>
                <Tabs.Content value="org">
                    <div class="mb-2 flex items-end gap-2">
                        <div class="flex-1">
                            <Select.Root type="single" bind:value={selectedOrgId}>
                                <Select.Trigger class="w-full">
                                    {orgsQuery.data?.data?.find((o) => o.id === selectedOrgId)?.name ?? 'Select organization...'}
                                </Select.Trigger>
                                <Select.Content>
                                    {#each orgsQuery.data?.data ?? [] as org (org.id)}
                                        <Select.Item value={org.id}>{org.name}</Select.Item>
                                    {/each}
                                </Select.Content>
                            </Select.Root>
                        </div>
                        <Button
                            disabled={!selectedOrgId || orgExport.isPending}
                            onclick={() => { void handleLoadOrgViews(); }}
                            size="sm"
                            variant="outline"
                        >
                            {#if orgExport.isPending}
                                <Spinner />
                                Loading...
                            {:else}
                                Export Org Views
                            {/if}
                        </Button>
                    </div>
                    <Muted class="mb-2">Export an organization's saved views as predefined definitions, then save below.</Muted>
                </Tabs.Content>
            </Tabs.Root>
            <div class="flex justify-end gap-2">
                <CopyToClipboardButton value={predefinedJson} size="sm" variant="outline">Copy JSON</CopyToClipboardButton>
            </div>
            <Textarea
                bind:value={predefinedJson}
                class="font-mono text-xs max-h-[50vh] min-h-50 overflow-auto"
                rows={20}
            />
            <Dialog.Footer>
                <Button
                    variant="outline"
                    onclick={() => { openPredefinedDialog = false; }}
                >
                    Close
                </Button>
                <Button
                    disabled={savePredefined.isPending || !predefinedJson.trim()}
                    onclick={() => { void handleSavePredefined(); }}
                >
                    {#if savePredefined.isPending}
                        <Spinner />
                        Saving...
                    {:else}
                        Save as Predefined
                    {/if}
                </Button>
            </Dialog.Footer>
        </Dialog.Content>
    </Dialog.Root>
{/if}
