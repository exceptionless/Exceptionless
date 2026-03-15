<script lang="ts">
    import type { ShardMetric } from '$features/admin/models';
    import type { TableMemoryPagingParameters } from '$features/shared/table.svelte';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import Bytes from '$comp/formatters/bytes.svelte';
    import Number from '$comp/formatters/number.svelte';
    import { Badge } from '$comp/ui/badge';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import { getElasticsearchQuery } from '$features/admin/api.svelte';
    import ShardsOverviewDataTable from '$features/admin/components/table/shards-overview-data-table.svelte';
    import { getTableOptions } from '$features/admin/components/table/shards-overview-options.svelte';
    import { healthBadgeClass, healthColor, healthLabel, healthVariant } from '$features/admin/elasticsearch-utils';
    import CircleCheck from '@lucide/svelte/icons/circle-check';
    import Database from '@lucide/svelte/icons/database';
    import HardDrive from '@lucide/svelte/icons/hard-drive';
    import Server from '@lucide/svelte/icons/server';
    import TriangleAlert from '@lucide/svelte/icons/triangle-alert';
    import { createTable } from '@tanstack/svelte-table';

    const esQuery = getElasticsearchQuery();
    const data = $derived(esQuery.data);

    const shardMetrics = $derived<ShardMetric[]>(
        data
            ? [
                  { id: 'active_primary', label: 'Active Primary Shards', value: data.health.active_primary_shards },
                  { id: 'active_total', label: 'Active Shards (Total)', value: data.health.active_shards },
                  { id: 'relocating', label: 'Relocating Shards', value: data.health.relocating_shards },
                  { id: 'unassigned', label: 'Unassigned Shards', value: data.health.unassigned_shards }
              ]
            : []
    );

    const queryParameters: TableMemoryPagingParameters = $state({ limit: 10 });
    const shardTable = createTable(getTableOptions(queryParameters, () => shardMetrics));

    function shardRowClick(metric: ShardMetric) {
        if (metric.id === 'unassigned') {
            goto(resolve('/(app)/system/elasticsearch/indices'));
        }
    }
</script>

{#if esQuery.isPending}
    <div class="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
        {#each [1, 2, 3, 4] as i (i)}
            <Card.Root>
                <Card.Content class="pt-6">
                    <Skeleton class="mb-2 h-8 w-20" />
                    <Skeleton class="h-4 w-28" />
                </Card.Content>
            </Card.Root>
        {/each}
    </div>
{:else if esQuery.isError}
    <Card.Root>
        <Card.Content class="pt-6">
            <p class="text-destructive text-sm">Failed to load Elasticsearch info. Ensure you have admin access.</p>
        </Card.Content>
    </Card.Root>
{:else if data}
    <div class="space-y-4">
        <div class="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            <Card.Root>
                <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                    <Card.Title class="text-sm font-medium">Cluster Status</Card.Title>
                    {#if data.health.status === 0}
                        <CircleCheck class="text-muted-foreground size-4" />
                    {:else}
                        <TriangleAlert class="size-4 {healthColor(data.health.status)}" />
                    {/if}
                </Card.Header>
                <Card.Content>
                    <div class="flex items-center gap-2">
                        <Badge class={healthBadgeClass(data.health.status)} variant={healthVariant(data.health.status)}>
                            {healthLabel(data.health.status)}
                        </Badge>
                    </div>
                    <p class="text-muted-foreground mt-1 text-xs">{data.health.cluster_name}</p>
                </Card.Content>
            </Card.Root>

            <Card.Root>
                <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                    <Card.Title class="text-sm font-medium">Nodes</Card.Title>
                    <Server class="text-muted-foreground size-4" />
                </Card.Header>
                <Card.Content>
                    <div class="text-2xl font-bold">{data.health.number_of_nodes}</div>
                    <p class="text-muted-foreground text-xs">
                        {data.health.number_of_data_nodes} data node{data.health.number_of_data_nodes !== 1 ? 's' : ''}
                    </p>
                </Card.Content>
            </Card.Root>

            <Card.Root>
                <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                    <Card.Title class="text-sm font-medium">Indices</Card.Title>
                    <Database class="text-muted-foreground size-4" />
                </Card.Header>
                <Card.Content>
                    <div class="text-2xl font-bold"><Number value={data.indices.count} /></div>
                    <p class="text-muted-foreground text-xs"><Number value={data.indices.docs_count} /> documents</p>
                </Card.Content>
            </Card.Root>

            <Card.Root>
                <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                    <Card.Title class="text-sm font-medium">Storage</Card.Title>
                    <HardDrive class="text-muted-foreground size-4" />
                </Card.Header>
                <Card.Content>
                    <div class="text-2xl font-bold"><Bytes value={data.indices.store_size_in_bytes} /></div>
                    <p class="text-muted-foreground text-xs">Total index size</p>
                </Card.Content>
            </Card.Root>
        </div>

        <div class="flex flex-col gap-2">
            <div>
                <p class="text-sm leading-none font-semibold tracking-tight">Shard Details</p>
                <p class="text-muted-foreground mt-1.5 text-sm">Allocation and status of Elasticsearch shards across the cluster</p>
            </div>
            <ShardsOverviewDataTable isLoading={esQuery.isPending} rowClick={shardRowClick} table={shardTable} />
        </div>
    </div>
{/if}
