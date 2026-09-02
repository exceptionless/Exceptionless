<script lang="ts">
    import type { ProductTourSummary, ProductTourUsageInterval } from '$generated/api';

    import Number from '$comp/formatters/number.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import * as Chart from '$comp/ui/chart';
    import * as Table from '$comp/ui/table';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { LineChart, Points, Spline } from 'layerchart';

    import { getProductTourActivity } from '../product-tour-usage';
    import ProductTourTotals from './product-tour-totals.svelte';

    let { end, interval, start, tour }: { end: string; interval: ProductTourUsageInterval; start?: null | string; tour: ProductTourSummary } = $props();
    const prompt = $derived(tour.kind === 'prompt');
    const data = $derived(getProductTourActivity(tour.activity ?? [], interval, start, end));
    const config = $derived({
        completed: {
            color: 'var(--chart-1)',
            label: prompt ? 'Accepted' : 'Completed'
        },
        dismissed: {
            color: 'var(--chart-5)',
            label: 'Dismissed'
        },
        shown: {
            color: 'var(--chart-4)',
            label: 'Shown'
        },
        started: {
            color: 'var(--chart-2)',
            label: 'Started'
        }
    } satisfies Chart.ChartConfig);
    const keys = $derived(prompt ? (['shown', 'started', 'completed', 'dismissed'] as const) : (['started', 'completed', 'dismissed'] as const));
    const series = $derived(
        keys.map((key) => ({
            key,
            ...config[key]
        }))
    );
    const total = $derived(tour.shown + tour.started + tour.completed + tour.dismissed);

    function sourceLabel(source: string): string {
        switch (source) {
            case 'catalog':
                return 'Guided Tours menu';
            case 'command-palette':
                return 'Command palette';
            case 'feature-announcement':
                return 'Exie invitation';
            case 'help-menu':
                return 'Help menu';
            case 'welcome':
                return 'Welcome invitation';
            default:
                return source;
        }
    }

    function dateLabel(value: unknown): string {
        if (!(value instanceof Date)) {
            return '';
        }
        return value.toLocaleDateString(undefined, {
            day: interval === 'day' ? 'numeric' : undefined,
            month: 'short',
            timeZone: 'UTC',
            year: interval === 'month' ? 'numeric' : undefined
        });
    }
</script>

<div class="flex flex-col gap-4">
    {#if total === 0}
        <p class="text-muted-foreground text-sm">No recorded activity in this period.</p>
    {:else}
        <Chart.Container
            {config}
            class="h-40 w-full"
            aria-label={`${interval === 'day' ? 'Daily' : 'Monthly'} ${prompt ? 'invitation' : 'guide'} activity. Open View details for totals and exact values.`}
        >
            <LineChart
                {data}
                x="date"
                xScale={scaleUtc()}
                {series}
                yDomain={[0, null]}
                padding={{
                    bottom: 48,
                    left: 32,
                    right: 32,
                    top: 8
                }}
                axis
                legend
                props={{
                    xAxis: {
                        format: dateLabel,
                        ticks: data.filter((_, index) => index % Math.max(1, Math.ceil((data.length - 1) / 4)) === 0).map((period) => period.date)
                    },
                    yAxis: {
                        format: (value) => String(value),
                        ticks: (scale) => scale.ticks?.(3).filter((value: number) => globalThis.Number.isInteger(value))
                    }
                }}
            >
                {#snippet marks({ context })}
                    {#each context.series.visibleSeries as item (item.key)}
                        <Spline seriesKey={item.key} curve={curveLinear} strokeWidth={2} />
                        <Points seriesKey={item.key} r={3} fill={item.color} />
                    {/each}
                {/snippet}
                {#snippet tooltip()}<Chart.Tooltip labelFormatter={dateLabel} indicator="line" />{/snippet}
            </LineChart>
        </Chart.Container>
        <details>
            <summary class="text-muted-foreground cursor-pointer text-xs">View details</summary>
            <div class="my-4 space-y-3">
                <ProductTourTotals {tour} />
                <p class="text-muted-foreground text-xs">Counts are events, not unique people. Rates compare events in this period.</p>
                {#if tour.start_sources.length > 0}
                    <div class="text-xs">
                        <p class="text-muted-foreground mb-1">How people opened this guide</p>
                        <ul class="flex flex-wrap gap-x-4 gap-y-1">
                            {#each tour.start_sources as source (source.source)}
                                <li>{sourceLabel(source.source)}: <Number value={source.count} /></li>
                            {/each}
                        </ul>
                    </div>
                {/if}
                {#if tour.last_run_utc}
                    <p class="text-muted-foreground text-xs">Last activity <TimeAgo value={tour.last_run_utc} /></p>
                {/if}
            </div>
            <Table.Root aria-label="Guide activity by date">
                <Table.Header
                    ><Table.Row
                        ><Table.Head>Date (UTC)</Table.Head>{#each keys as key (key)}<Table.Head class="text-right">{config[key].label}</Table.Head
                            >{/each}</Table.Row
                    ></Table.Header
                >
                <Table.Body
                    >{#each data as period (period.date_utc)}<Table.Row
                            ><Table.Cell>{dateLabel(period.date)}</Table.Cell>{#each keys as key (key)}<Table.Cell class="text-right"
                                    ><Number value={period[key]} /></Table.Cell
                                >{/each}</Table.Row
                        >{/each}</Table.Body
                >
            </Table.Root>
        </details>
    {/if}
</div>
