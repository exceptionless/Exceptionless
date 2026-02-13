<script lang="ts">
    import * as Chart from '$comp/ui/chart/index';
    import { Skeleton } from '$comp/ui/skeleton';
    import { formatDateLabel } from '$features/shared/dates';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { AreaChart } from 'layerchart';

    type SessionChartDataPoint = {
        date: Date;
        sessions: number;
        users: number;
    };

    let {
        class: className = '',
        data,
        isLoading = false,
        onRangeSelect
    }: {
        class?: string;
        data: SessionChartDataPoint[];
        isLoading?: boolean;
        onRangeSelect?: (start: Date, end: Date) => void;
    } = $props();

    const chartConfig = {
        sessions: {
            color: 'hsl(95, 59%, 48%)',
            label: 'Sessions'
        },
        users: {
            color: 'hsl(95, 59%, 23%)',
            label: 'Users'
        }
    } satisfies Chart.ChartConfig;

    const series = [
        {
            color: chartConfig.users.color,
            key: 'users',
            label: 'Users'
        },
        {
            color: chartConfig.sessions.color,
            key: 'sessions',
            label: 'Sessions'
        }
    ];
</script>

<div class="bg-card text-card-foreground rounded-lg border shadow-sm {className}">
    {#if isLoading}
        <Skeleton class="h-16 w-full rounded" />
    {:else}
        <Chart.Container config={chartConfig} class="h-16 w-full">
            <AreaChart
                {data}
                x="date"
                xScale={scaleUtc()}
                yDomain={[0, Math.max(1, Math.max(...data.map((d) => d.sessions)))]}
                {series}
                axis={false}
                grid={false}
                brush={{
                    onBrushEnd: (detail) => {
                        const [start, end] = detail.xDomain ?? [];
                        if (start instanceof Date && end instanceof Date) {
                            onRangeSelect?.(start, end);
                        }
                    }
                }}
                props={{
                    area: {
                        curve: curveLinear
                    },
                    canvas: {
                        class: 'cursor-crosshair'
                    },
                    svg: {
                        class: 'cursor-crosshair'
                    }
                }}
            >
                {#snippet tooltip()}
                    <Chart.Tooltip class="min-w-62.5" indicator="line" labelFormatter={(v) => formatDateLabel(v as Date)} />
                {/snippet}
            </AreaChart>
        </Chart.Container>
    {/if}
</div>
