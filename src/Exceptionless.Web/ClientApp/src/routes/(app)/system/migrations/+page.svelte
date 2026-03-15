<script lang="ts">
    import type { MigrationState, MigrationStatus } from '$features/admin/models';
    import type { TableMemoryPagingParameters } from '$features/shared/table.svelte';

    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import { H3, Muted } from '$comp/typography';
    import * as Card from '$comp/ui/card';
    import { Input } from '$comp/ui/input';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import { getMigrationsQuery } from '$features/admin/api.svelte';
    import MigrationsDataTable from '$features/admin/components/table/migrations-data-table.svelte';
    import { getTableOptions } from '$features/admin/components/table/migrations-options.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import CheckCircle2 from '@lucide/svelte/icons/check-circle-2';
    import Layers from '@lucide/svelte/icons/layers';
    import Loader from '@lucide/svelte/icons/loader';
    import XCircle from '@lucide/svelte/icons/x-circle';
    import { createTable } from '@tanstack/svelte-table';

    const migrationsQuery = getMigrationsQuery();
    const data = $derived(migrationsQuery.data);

    let showCompleted = $state(true);
    let searchQuery = $state('');

    function getStatus(state: MigrationState): MigrationStatus {
        if (state.error_message) return 'Failed';
        if (state.completed_utc) return 'Completed';
        if (state.started_utc) return 'Running';
        return 'Pending';
    }

    const allStates = $derived((data?.states ?? []).map((s) => ({ ...s, status: getStatus(s) })));
    const failedCount = $derived(allStates.filter((s) => s.status === 'Failed').length);
    const runningCount = $derived(allStates.filter((s) => s.status === 'Running').length);

    const filteredStates = $derived(
        allStates.filter((state) => {
            const matchesCompleted = showCompleted || state.status !== 'Completed';
            const query = searchQuery.trim().toLowerCase();
            const matchesSearch =
                query === '' ||
                state.id.toLowerCase().includes(query) ||
                String(state.version ?? '').includes(query) ||
                state.status.toLowerCase().includes(query);
            return matchesCompleted && matchesSearch;
        })
    );

    const queryParameters: TableMemoryPagingParameters = $state({ limit: DEFAULT_LIMIT });
    const table = createTable(getTableOptions(queryParameters, () => filteredStates));
</script>

<div class="space-y-6">
    <div>
        <H3>Migrations</H3>
        <Muted>Database migration history — versioned schema changes applied to Elasticsearch.</Muted>
    </div>
    <Separator />

    {#if migrationsQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load migration history. Please try again.</p>
            </Card.Content>
        </Card.Root>
    {:else}
        {#if migrationsQuery.isPending}
            <div class="grid grid-cols-2 gap-4 sm:grid-cols-4">
                {#each [1, 2, 3, 4] as i (i)}
                    <Card.Root>
                        <Card.Content class="pt-6">
                            <Skeleton class="mb-2 h-8 w-16" />
                            <Skeleton class="h-4 w-24" />
                        </Card.Content>
                    </Card.Root>
                {/each}
            </div>
        {:else if data}
            <div class="grid grid-cols-2 gap-4 sm:grid-cols-4">
                <Card.Root>
                    <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                        <Card.Title class="text-sm font-medium">Current Version</Card.Title>
                        <CheckCircle2 class="text-muted-foreground size-4" />
                    </Card.Header>
                    <Card.Content>
                        <div class="text-2xl font-bold">{data.current_version >= 0 ? data.current_version : '—'}</div>
                        <Muted>Highest completed versioned</Muted>
                    </Card.Content>
                </Card.Root>
                <Card.Root>
                    <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                        <Card.Title class="text-sm font-medium">Failed</Card.Title>
                        <XCircle class="text-muted-foreground size-4" />
                    </Card.Header>
                    <Card.Content>
                        <div class="text-2xl font-bold" class:text-destructive={failedCount > 0}>{failedCount}</div>
                        <Muted>{failedCount > 0 ? 'Requires attention' : 'No failures'}</Muted>
                    </Card.Content>
                </Card.Root>
                <Card.Root>
                    <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                        <Card.Title class="text-sm font-medium">Running</Card.Title>
                        <Loader class="text-muted-foreground size-4" />
                    </Card.Header>
                    <Card.Content>
                        <div class="text-2xl font-bold" class:text-blue-500={runningCount > 0}>{runningCount}</div>
                        <Muted>In progress</Muted>
                    </Card.Content>
                </Card.Root>
                <Card.Root>
                    <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                        <Card.Title class="text-sm font-medium">Total</Card.Title>
                        <Layers class="text-muted-foreground size-4" />
                    </Card.Header>
                    <Card.Content>
                        <div class="text-2xl font-bold">{allStates.length}</div>
                        <Muted>State records in ES</Muted>
                    </Card.Content>
                </Card.Root>
            </div>
        {/if}

        <MigrationsDataTable bind:limit={queryParameters.limit!} isLoading={migrationsQuery.isPending} {table}>
            {#snippet toolbarChildren()}
                <Input bind:value={searchQuery} class="flex-1" placeholder="Search migrations..." type="search" />
                <label class="text-muted-foreground flex cursor-pointer items-center gap-2 text-sm" for="show-completed">
                    <Switch id="show-completed" bind:checked={showCompleted} />
                    Show completed
                </label>
                <DataTableViewOptions size="icon-lg" {table} />
            {/snippet}
        </MigrationsDataTable>
    {/if}
</div>
