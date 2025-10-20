<script lang="ts">
    import * as Chart from '$comp/ui/chart/index';
    import { Skeleton } from '$comp/ui/skeleton';
    import { formatDateLabel } from '$features/shared/dates';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { AreaChart } from 'layerchart';

    export type EventsStackChartPoint = {
        date: Date;
        occurrences: number;
    };

    let {
        class: className = '',
        data,
        isLoading = false
    }: {
        class?: string;
        data: EventsStackChartPoint[];
        isLoading?: boolean;
    } = $props();

    const chartConfig = {
        occurrences: {
            color: 'var(--chart-1)',
            label: 'Events'
        }
    } satisfies Chart.ChartConfig;

    const series = [
        {
            color: chartConfig.occurrences.color,
            key: 'occurrences',
            label: 'Events'
        }
    ];
</script>

<div class="bg-card text-card-foreground rounded-lg border shadow-sm {className}">
    {#if isLoading}
        <Skeleton class="h-full w-full rounded-md" />
    {:else}
        <Chart.Container config={chartConfig} class="h-full w-full">
            <AreaChart
                {data}
                x="date"
                xScale={scaleUtc()}
                yDomain={[0, Math.max(1, Math.max(...data.map(d => d.occurrences)))]}
                {series}
                axis={false}
                grid={false}
                props={{
                    area: {
                        curve: curveLinear
                    },
                    canvas: {
                        class: 'cursor-default'
                    },
                    svg: {
                        class: 'cursor-default'
                    }
                }}
            >
                {#snippet tooltip()}
                    <Chart.Tooltip class="min-w-[160px]" indicator="line" labelFormatter={(value: Date) => formatDateLabel(value)} />
                {/snippet}
            </AreaChart>
        </Chart.Container>
    {/if}
</div>
