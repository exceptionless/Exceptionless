<script lang="ts">
    import type { EventChart, EventChartResult } from '$generated/api';

    import * as Chart from '$comp/ui/chart';
    import { Skeleton } from '$comp/ui/skeleton';
    import { formatDateLabel } from '$features/shared/dates';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { BarChart, LineChart, Points, Spline } from 'layerchart';

    import { chartSeries, formatMeasurement, type MeasurementPoint, measurementUnit } from '../event-chart';

    let {
        chart,
        isLoading,
        onSelect,
        result
    }: {
        chart: EventChart;
        isLoading: boolean;
        onSelect: (point: MeasurementPoint, series: string) => void;
        result?: EventChartResult;
    } = $props();

    const series = $derived(chartSeries(result));
    const data = $derived(series.flatMap((s) => s.data));
    const config: Chart.ChartConfig = $derived(Object.fromEntries(series.map((item) => [item.key, item])));
    const chartProps = {
        spline: {
            curve: curveLinear,
            defined: (point: MeasurementPoint) => point.value != null
        }
    };
    const unit = $derived(measurementUnit(chart));
    function selectAt(date: unknown, key: string, value: unknown) {
        const selectedSeries = series.find((s) => s.key === key);
        const timestamp = date instanceof Date ? date.getTime() : Number(date);
        const point = selectedSeries?.data.find((point) => point.date.getTime() === timestamp && point.value === value);
        if (point && point.value != null) {
            onSelect(point, selectedSeries!.label);
        }
    }
</script>

<Chart.Shell>
    <p class="mb-2 text-sm font-medium">
        {chart.measurement ?? 'Events'} · {chart.mode === 'events' ? 'Individual events' : chart.aggregation}
        {unit === '1' ? '' : `(${unit})`}
    </p>
    {#if isLoading}
        <Skeleton class="h-64 w-full" />
    {:else if !data.some((point) => point.value != null)}
        <p class="text-muted-foreground py-12 text-center text-sm">No matching observations in this time range.</p>
    {:else}
        <Chart.Container {config} class="h-64 w-full">
            {#if chart.display === 'bar'}
                <BarChart
                    {data}
                    x="date"
                    {series}
                    legend
                    seriesLayout="group"
                    onBarClick={(_, details) => selectAt(details.data.date, details.series.key, details.data.value)}
                >
                    {#snippet tooltip()}{@render chartTooltip()}{/snippet}
                </BarChart>
            {:else}
                <LineChart
                    {data}
                    x="date"
                    xScale={scaleUtc()}
                    {series}
                    legend
                    onPointClick={(_, details) => selectAt(details.data.x, details.series.key, details.data.y)}
                >
                    {#snippet marks({ context })}
                        {#each context.series.visibleSeries as item (item.key)}
                            <Spline seriesKey={item.key} {...chartProps.spline} />
                            <Points seriesKey={item.key} r={3} />
                        {/each}
                    {/snippet}
                    {#snippet tooltip()}{@render chartTooltip()}{/snippet}
                </LineChart>
            {/if}
        </Chart.Container>
        <p class="text-muted-foreground mt-2 text-xs">Select a point to inspect its events. Missing measurements appear as gaps.</p>
        <details class="mt-2 text-sm">
            <summary class="cursor-pointer">View observations</summary>
            <div class="max-h-64 overflow-auto">
                {#each series as item (item.key)}
                    <p class="mt-2 font-medium">{item.label}</p>
                    {#each item.data as point, index (`${point.date.getTime()}-${index}`)}
                        <button
                            class="hover:bg-muted flex w-full justify-between gap-4 rounded p-1 text-left"
                            disabled={point.value == null}
                            onclick={() => onSelect(point, item.label)}
                        >
                            <span>{formatDateLabel(point.date)}</span><span>{formatMeasurement(point.value, unit)}</span>
                        </button>
                    {/each}
                {/each}
            </div>
        </details>
    {/if}
    {#if result?.truncated}<p role="status" class="text-muted-foreground mt-2 text-sm">
            Showing a limited set of observations or series. Narrow the filter or time range for a complete view. Events without the split field are excluded
            from bucketed series.
        </p>{/if}
</Chart.Shell>

{#snippet chartTooltip()}
    <Chart.Tooltip labelFormatter={(value) => formatDateLabel(value instanceof Date ? value : new Date(value as number | string))}>
        {#snippet formatter({ name, value })}<span>{name}</span><span>{formatMeasurement(typeof value === 'number' ? value : null, unit)}</span>{/snippet}
    </Chart.Tooltip>
{/snippet}
