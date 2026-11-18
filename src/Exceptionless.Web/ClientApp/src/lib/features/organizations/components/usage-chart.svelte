<script lang="ts">
    import { scaleUtc } from 'd3-scale';
    import { curveMonotoneX } from 'd3-shape';
    import { AreaChart } from 'layerchart';

    import type { UsageInfo } from '#generated/api.ts';

    import * as Chart from '#comp/ui/chart/index.ts';
    import { formatDateLabel } from '#shared/dates.ts';

    let {
        data,
        showOrganizationTotal = false
    }: {
        data: (Omit<UsageInfo, 'date'> & { date: Date; organization_total?: number })[];
        showOrganizationTotal?: boolean;
    } = $props();

    const chartConfig = {
        blocked: {
            color: 'var(--chart-2)',
            label: 'Blocked'
        },
        deleted: {
            color: 'var(--chart-7)',
            label: 'Deleted'
        },
        discarded: {
            color: 'var(--chart-3)',
            label: 'Discarded'
        },
        limit: {
            color: 'var(--chart-6)',
            label: 'Limit'
        },
        organization_total: {
            color: 'var(--chart-5)',
            label: 'Total in Organization'
        },
        too_big: {
            color: 'var(--chart-4)',
            label: 'Too Big'
        },
        total: {
            color: 'var(--chart-1)',
            label: 'Total'
        }
    } satisfies Chart.ChartConfig;

    const series = $derived([
        ...(showOrganizationTotal
            ? [
                  {
                      key: 'organization_total',
                      ...chartConfig.organization_total
                  }
              ]
            : []),
        {
            key: 'total',
            ...chartConfig.total
        },
        {
            key: 'discarded',
            ...chartConfig.discarded
        },
        {
            key: 'blocked',
            ...chartConfig.blocked
        },
        {
            key: 'too_big',
            ...chartConfig.too_big
        },
        {
            key: 'deleted',
            ...chartConfig.deleted
        },
        {
            key: 'limit',
            ...chartConfig.limit,
            props: {
                class: 'usage-limit-line fill-none'
            }
        }
    ]);
</script>

<Chart.Container config={chartConfig} class="aspect-auto h-62.5 w-full">
    <AreaChart
        legend
        {data}
        x="date"
        xScale={scaleUtc()}
        yDomain={showOrganizationTotal
            ? undefined
            : [0, Math.max(1, ...data.map((d) => Math.max(d.total, d.limit, d.blocked, d.discarded, d.too_big, d.deleted)))]}
        seriesLayout="overlap"
        {series}
        props={{
            area: {
                curve: curveMonotoneX
            },
            yAxis: {
                format: 'metric'
            }
        }}
    >
        {#snippet tooltip()}
            <!-- Preserve the tooltip order used by the original stacked charts. -->
            <Chart.Tooltip reverse class="min-w-57.5" indicator="line" labelFormatter={(v) => formatDateLabel(v as Date)} />
        {/snippet}
    </AreaChart>
</Chart.Container>
