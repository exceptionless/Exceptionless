<script lang="ts">
    import * as Chart from '$comp/ui/chart/index';
    import { Skeleton } from '$comp/ui/skeleton';
    import { formatDateLabel } from '$features/shared/dates';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { AreaChart } from 'layerchart';

    type ChartDataPoint = {
        date: Date;
        events: number;
        stacks: number;
    };

    let {
        class: className = '',
        data,
        isLoading = false,
        onRangeSelect
    }: {
        class?: string;
        data: ChartDataPoint[];
        isLoading?: boolean;
        onRangeSelect?: (start: Date, end: Date) => void;
    } = $props();

    const chartConfig = {
        events: {
            color: 'var(--chart-1)',
            label: 'Events'
        },
        stacks: {
            color: 'var(--chart-2)',
            label: 'Unique Events (Stacks)'
        }
    } satisfies Chart.ChartConfig;

    const series = [
        {
            color: chartConfig.stacks.color,
            key: 'stacks',
            label: 'Unique Events (Stacks)'
        },
        {
            color: chartConfig.events.color,
            key: 'events',
            label: 'Events'
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
                    <Chart.Tooltip class="min-w-[250px]" indicator="line" labelFormatter={(v: Date) => formatDateLabel(v)} />
                {/snippet}
            </AreaChart>
        </Chart.Container>
    {/if}
</div>
