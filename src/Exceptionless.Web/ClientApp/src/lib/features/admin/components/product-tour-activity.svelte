<script lang="ts">
    import type { ProductTourSummary, ProductTourUsageInterval } from '$generated/api';

    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';
    import * as Chart from '$comp/ui/chart';
    import * as Table from '$comp/ui/table';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { LineChart } from 'layerchart';

    import { getProductTourActivity, getRate } from '../product-tour-usage';

    let { end, interval, start, tour }: { end: string; interval: ProductTourUsageInterval; start?: null | string; tour: ProductTourSummary } = $props();
    const prompt = $derived(tour.kind === 'prompt');
    const data = $derived(getProductTourActivity(tour.activity ?? [], interval, start, end));
    const config = $derived({
        completed: {
            color: 'var(--chart-1)',
            label: prompt ? 'Engaged' : 'Completed'
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
    <dl class="grid grid-cols-2 gap-3 sm:grid-cols-4" aria-label="Selected guide totals">
        {#if !prompt}
            <div>
                <dt class="text-muted-foreground text-xs">Recorded events</dt>
                <dd class="text-xl font-semibold"><Number value={total} /></dd>
            </div>
        {/if}
        {#each keys as key (key)}
            {@const rate = key === 'shown' || (!prompt && key === 'started') ? undefined : getRate(tour[key], prompt ? tour.shown : tour.started)}
            <div>
                <dt class="text-muted-foreground text-xs">{config[key].label}</dt>
                <dd class="text-xl font-semibold"><Number value={tour[key]} /></dd>
                {#if rate !== undefined}<dd class="text-muted-foreground text-xs">
                        {#if rate === null}—{:else}<Percentage percent={rate * 100} />{/if} of {prompt ? 'shown' : 'starts'}
                    </dd>{/if}
            </div>
        {/each}
    </dl>
    <p class="text-muted-foreground text-xs">{interval === 'day' ? 'Daily' : 'Monthly'} activity · UTC · Counts are events, not unique users.</p>
    {#if total === 0}
        <p class="text-muted-foreground flex h-40 items-center justify-center text-sm">No activity recorded for this guide in this period.</p>
    {:else}
        <Chart.Container
            {config}
            class="h-40 w-full"
            aria-label={`${interval === 'day' ? 'Daily' : 'Monthly'} guide activity. Exact values are in the activity table below.`}
        >
            <LineChart
                {data}
                x="date"
                xScale={scaleUtc()}
                {series}
                yDomain={[0, null]}
                axis
                legend
                props={{
                    spline: {
                        curve: curveLinear,
                        strokeWidth: 2
                    },
                    xAxis: {
                        format: dateLabel,
                        ticks: 4
                    }
                }}
            >
                {#snippet tooltip()}<Chart.Tooltip labelFormatter={dateLabel} indicator="line" />{/snippet}
            </LineChart>
        </Chart.Container>
        <details>
            <summary class="text-muted-foreground cursor-pointer text-xs">View activity table</summary>
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
