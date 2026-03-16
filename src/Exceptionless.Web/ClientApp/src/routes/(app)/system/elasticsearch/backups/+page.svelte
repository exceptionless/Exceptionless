<script lang="ts">
    import type { TableMemoryPagingParameters } from '$features/shared/table.svelte';

    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import { getElasticsearchSnapshotsQuery } from '$features/admin/api.svelte';
    import BackupsDataTable from '$features/admin/components/table/backups-data-table.svelte';
    import { getTableOptions } from '$features/admin/components/table/backups-options.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import Archive from '@lucide/svelte/icons/archive';
    import { createTable } from '@tanstack/svelte-table';

    const snapshotsQuery = getElasticsearchSnapshotsQuery();
    const snapshotsData = $derived(snapshotsQuery.data);

    const queryParameters: TableMemoryPagingParameters = $state({
        limit: DEFAULT_LIMIT
    });

    const table = createTable(getTableOptions(queryParameters, () => snapshotsData?.snapshots ?? []));
</script>

{#if snapshotsQuery.isPending}
    <Card.Root>
        <Card.Header>
            <Card.Title>Snapshot Backups</Card.Title>
            <Card.Description>Loading snapshot repositories...</Card.Description>
        </Card.Header>
        <Card.Content class="space-y-2 p-6">
            {#each [1, 2, 3, 4, 5] as i (i)}
                <Skeleton class="h-12 w-full rounded" />
            {/each}
        </Card.Content>
    </Card.Root>
{:else if snapshotsQuery.isError}
    <Card.Root>
        <Card.Content class="pt-6">
            <p class="text-destructive text-sm">Failed to load snapshot information. Please try again.</p>
        </Card.Content>
    </Card.Root>
{:else if !snapshotsData || snapshotsData.repositories.length === 0}
    <Card.Root>
        <Card.Content class="flex flex-col items-center gap-3 py-12">
            <Archive class="text-muted-foreground size-8" />
            <p class="text-muted-foreground text-sm">No snapshot repositories configured.</p>
            <p class="text-muted-foreground text-xs">
                Snapshot repositories are typically named <code class="font-mono">&lt;scope&gt;-hourly</code> (e.g.
                <code class="font-mono">prod-hourly</code>).
            </p>
        </Card.Content>
    </Card.Root>
{:else}
    <p class="text-muted-foreground text-sm">
        {snapshotsData.snapshots.length} snapshots across {snapshotsData.repositories.length}
        {snapshotsData.repositories.length === 1 ? 'repository' : 'repositories'}: {snapshotsData.repositories.join(', ')}
    </p>
    <BackupsDataTable bind:limit={queryParameters.limit!} isLoading={snapshotsQuery.isPending} {table} />
{/if}
