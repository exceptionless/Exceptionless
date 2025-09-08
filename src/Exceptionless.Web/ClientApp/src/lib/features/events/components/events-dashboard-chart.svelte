<script lang="ts">
    import * as Chart from '$comp/ui/chart/index';
    import { scaleUtc } from 'd3-scale';
    import { curveNatural } from 'd3-shape';
    import { AreaChart } from 'layerchart';

    type ChartDataPoint = {
        date: Date;
        events: number;
        stacks: number;
    };

    let {
        class: className = '',
        data,
        isLoading = false
    }: {
        class?: string;
        data: ChartDataPoint[];
        isLoading?: boolean;
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

<div class="rounded-lg border bg-card text-card-foreground shadow-sm {className}">
    {#if isLoading}
        <div class="bg-muted aspect-auto h-[90px] w-full animate-pulse rounded"></div>
    {:else}
        <Chart.Container config={chartConfig} class="aspect-auto h-[90px] w-full p-0">
            <AreaChart
                {data}
                x="date"
                xScale={scaleUtc()}
                {series}
                axis={false}
                grid={false}
                props={{
                    area: {
                        curve: curveNatural
                    }
                }}
            >
                {#snippet tooltip()}
                    <Chart.Tooltip
                        class="min-w-[250px]"
                        indicator="line"
                        labelFormatter={(v: Date) => {
                            return v.toLocaleDateString('en-US', {
                                day: 'numeric',
                                month: 'short'
                            });
                        }}
                    />
                {/snippet}
            </AreaChart>
        </Chart.Container>
    {/if}
</div>
