<script lang="ts">
    import Number from '$comp/formatters/number.svelte';
    import { H3, Muted } from '$comp/typography';
    import * as Card from '$comp/ui/card';
    import * as Chart from '$comp/ui/chart/index';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import { getAdminStatsQuery } from '$features/admin/api.svelte';
    import { dateHistogram, terms } from '$features/shared/api/aggregations';
    import Activity from '@lucide/svelte/icons/activity';
    import Building2 from '@lucide/svelte/icons/building-2';
    import FolderOpen from '@lucide/svelte/icons/folder-open';
    import Layers from '@lucide/svelte/icons/layers';
    import ShieldAlert from '@lucide/svelte/icons/shield-alert';
    import Users from '@lucide/svelte/icons/users';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { AreaChart, BarChart } from 'layerchart';

    const statsQuery = getAdminStatsQuery();
    const stats = $derived(statsQuery.data);

    // Parse aggregations from raw CountResult objects
    const billingStatusBuckets = $derived(terms(stats?.organizations.aggregations, 'terms_billing_status')?.buckets ?? []);
    const stackStatusBuckets = $derived(terms(stats?.stacks.aggregations, 'terms_status')?.buckets ?? []);
    const stackTypeStatusBuckets = $derived(terms(stats?.stacks.aggregations, 'terms_type')?.buckets ?? []);

    const openStackCount = $derived(stackStatusBuckets.filter((b) => b.key === 'open' || b.key === 'regressed').reduce((sum, b) => sum + (b.total ?? 0), 0));

    const billingBreakdown = $derived(billingStatusBuckets.map((b) => `${b.total ?? 0} ${b.key}`).join(', '));

    const stackStatusBreakdown = $derived(
        stackStatusBuckets.map((bucket) => ({
            status: String(bucket.key),
            total: bucket.total ?? 0
        }))
    );

    const statCards = $derived([
        {
            icon: Building2,
            label: 'Organizations',
            sub: billingBreakdown,
            value: stats?.organizations.total
        },
        {
            icon: Users,
            label: 'Users',
            sub: undefined,
            value: stats?.users.total
        },
        {
            icon: FolderOpen,
            label: 'Projects',
            sub: undefined,
            value: stats?.projects.total
        },
        {
            icon: Layers,
            label: 'Total Stacks',
            sub: undefined,
            value: stats?.stacks.total
        },
        {
            icon: ShieldAlert,
            label: 'Open Stacks',
            sub: undefined,
            value: openStackCount
        },
        {
            icon: Activity,
            label: 'Events All-Time',
            sub: undefined,
            value: stats?.events.total
        }
    ]);

    const eventsAllTimeChartConfig = {
        count: { color: 'var(--chart-1)', label: 'Events' }
    } satisfies Chart.ChartConfig;

    const eventsAllTimeChartSeries = [{ color: eventsAllTimeChartConfig.count.color, key: 'count', label: 'Events' }];

    const eventsAllTimeChartData = $derived(
        (dateHistogram(stats?.events.aggregations, 'date_date')?.buckets ?? []).map((b) => ({
            count: b.total ?? 0,
            date: new Date(b.date)
        }))
    );

    const organizationGrowthChartConfig = {
        count: { color: 'var(--chart-2)', label: 'New Organizations' }
    } satisfies Chart.ChartConfig;

    const organizationGrowthChartSeries = [{ color: organizationGrowthChartConfig.count.color, key: 'count', label: 'New Organizations' }];

    const organizationGrowthChartData = $derived(
        (dateHistogram(stats?.organizations.aggregations, 'date_created_utc')?.buckets ?? []).map((b) => ({
            count: b.total ?? 0,
            month: new Date(b.date)
        }))
    );

    function formatMonthLabel(v: unknown): string {
        if (v instanceof Date) {
            return v.toLocaleDateString(undefined, { month: 'short', year: 'numeric' });
        }

        return String(v);
    }

    const statusColorMap: Record<string, string> = {
        discarded: 'var(--chart-5)',
        fixed: 'var(--chart-2)',
        ignored: 'var(--chart-4)',
        open: 'var(--chart-1)',
        regressed: 'var(--chart-3)',
        snoozed: 'var(--chart-5)'
    };

    const typeStatusChartData = $derived(
        stackTypeStatusBuckets.map((typeBucket) => {
            const statusBuckets = terms(typeBucket.aggregations, 'terms_status')?.buckets ?? [];
            const row: Record<string, number | string> = { type: (typeBucket.key as string) || '(none)' };
            for (const s of statusBuckets) {
                row[s.key as string] = s.total ?? 0;
            }

            return row;
        })
    );

    const allStatuses = $derived.by(() => {
        const statuses: string[] = [];
        for (const typeBucket of stackTypeStatusBuckets) {
            const statusBuckets = terms(typeBucket.aggregations, 'terms_status')?.buckets ?? [];
            for (const s of statusBuckets) {
                const key = s.key as string;
                if (!statuses.includes(key)) {
                    statuses.push(key);
                }
            }
        }

        return statuses.sort();
    });

    const typeStatusChartConfig = $derived(
        Object.fromEntries(allStatuses.map((status: string) => [status, { color: statusColorMap[status] ?? 'var(--chart-1)', label: status }])) as Record<
            string,
            { color: string; label: string }
        >
    );

    const typeStatusChartSeries = $derived(
        allStatuses.map((status: string) => ({ color: statusColorMap[status] ?? 'var(--chart-1)', key: status, label: status }))
    );
</script>

<div class="space-y-6">
    <div>
        <H3>Overview</H3>
        <Muted>System-wide statistics and usage trends.</Muted>
    </div>
    <Separator />

    {#if statsQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load system statistics. Please try again.</p>
            </Card.Content>
        </Card.Root>
    {:else}
        <div class="grid grid-cols-2 gap-4 sm:grid-cols-3">
            {#each statCards as card (card.label)}
                {@const Icon = card.icon}
                <Card.Root class="flex flex-col justify-between">
                    <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                        <Card.Title class="text-sm font-medium">{card.label}</Card.Title>
                        <Icon class="text-muted-foreground size-4" />
                    </Card.Header>
                    <Card.Content>
                        {#if statsQuery.isPending}
                            <div class="bg-muted h-8 w-24 animate-pulse rounded"></div>
                        {:else}
                            <div class="text-2xl font-bold"><Number value={card.value ?? null} /></div>
                            {#if card.label === 'Total Stacks' && stackStatusBreakdown.length > 0}
                                <p class="text-muted-foreground mt-0.5 text-xs leading-snug">
                                    {#each stackStatusBreakdown as bucket, index (bucket.status)}
                                        <Number value={bucket.total} /> {bucket.status}{index < stackStatusBreakdown.length - 1 ? ', ' : ''}
                                    {/each}
                                </p>
                            {:else if card.sub}
                                <p class="text-muted-foreground mt-0.5 text-xs leading-snug" title={card.sub}>{card.sub}</p>
                            {/if}
                        {/if}
                    </Card.Content>
                </Card.Root>
            {/each}
        </div>

        <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <Card.Root>
                <Card.Header>
                    <Card.Title class="text-sm font-medium">Events All-Time</Card.Title>
                    <Card.Description>Total event volume by month across the full history</Card.Description>
                </Card.Header>
                <Card.Content>
                    {#if statsQuery.isPending}
                        <Skeleton class="h-48 w-full rounded" />
                    {:else if eventsAllTimeChartData.length === 0}
                        <p class="text-muted-foreground py-8 text-center text-sm">No event history available.</p>
                    {:else}
                        <Chart.Container config={eventsAllTimeChartConfig} class="h-48 w-full">
                            <AreaChart
                                data={eventsAllTimeChartData}
                                x="date"
                                xScale={scaleUtc()}
                                yDomain={[0, Math.max(1, ...eventsAllTimeChartData.map((d) => d.count))]}
                                series={eventsAllTimeChartSeries}
                                props={{ area: { curve: curveLinear } }}
                            >
                                {#snippet tooltip()}
                                    <Chart.Tooltip indicator="line" labelFormatter={(v) => formatMonthLabel(v)} />
                                {/snippet}
                            </AreaChart>
                        </Chart.Container>
                    {/if}
                </Card.Content>
            </Card.Root>

            <Card.Root>
                <Card.Header>
                    <Card.Title class="text-sm font-medium">Organization Growth</Card.Title>
                    <Card.Description>New organizations created over time</Card.Description>
                </Card.Header>
                <Card.Content>
                    {#if statsQuery.isPending}
                        <Skeleton class="h-48 w-full rounded" />
                    {:else if organizationGrowthChartData.length === 0}
                        <p class="text-muted-foreground py-8 text-center text-sm">No growth data available.</p>
                    {:else}
                        <Chart.Container config={organizationGrowthChartConfig} class="h-48 w-full">
                            <AreaChart
                                data={organizationGrowthChartData}
                                x="month"
                                xScale={scaleUtc()}
                                yDomain={[0, Math.max(1, ...organizationGrowthChartData.map((d) => d.count))]}
                                series={organizationGrowthChartSeries}
                                props={{ area: { curve: curveLinear } }}
                            >
                                {#snippet tooltip()}
                                    <Chart.Tooltip indicator="line" labelFormatter={(v) => formatMonthLabel(v)} />
                                {/snippet}
                            </AreaChart>
                        </Chart.Container>
                    {/if}
                </Card.Content>
            </Card.Root>
        </div>

        {#if stackTypeStatusBuckets.length > 0 || statsQuery.isPending}
            <Card.Root>
                <Card.Header>
                    <Card.Title class="text-sm font-medium">Status by Event Type</Card.Title>
                    <Card.Description>Breakdown of stack statuses across each event type</Card.Description>
                </Card.Header>
                <Card.Content>
                    {#if statsQuery.isPending}
                        <Skeleton class="h-40 w-full rounded" />
                    {:else}
                        <Chart.Container config={typeStatusChartConfig} class="h-40 w-full">
                            <BarChart data={typeStatusChartData} x="type" series={typeStatusChartSeries} bandPadding={0.3}>
                                {#snippet tooltip()}
                                    <Chart.Tooltip indicator="dot" />
                                {/snippet}
                            </BarChart>
                        </Chart.Container>
                    {/if}
                </Card.Content>
            </Card.Root>
        {/if}
    {/if}
</div>
