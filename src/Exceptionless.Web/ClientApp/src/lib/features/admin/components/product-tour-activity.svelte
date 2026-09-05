<script lang="ts">
    import type { ProductTourSummary } from '$generated/api';

    import Number from '$comp/formatters/number.svelte';
    import * as Typography from '$comp/typography';
    import * as Chart from '$comp/ui/chart';
    import * as Table from '$comp/ui/table';
    import { formatDateLabel } from '$features/shared/dates';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { type ChartState, LineChart, Points, Spline } from 'layerchart';

    import { getProductTourActivity } from '../product-tour-usage';

    let { end, start, tour }: { end: string; start?: null | string; tour: ProductTourSummary } = $props();
    const prompt = $derived(tour.kind === 'prompt');
    const keyboardHelpId = $props.id();
    const data = $derived(getProductTourActivity(tour.activity ?? [], start, end));
    const config = $derived({
        completed: {
            color: 'color-mix(in srgb, var(--chart-1) var(--tour-chart-strength), black)',
            label: prompt ? 'Accepted' : 'Completed'
        },
        dismissed: {
            color: 'color-mix(in srgb, var(--chart-6) var(--tour-chart-strength), black)',
            label: 'Dismissed'
        },
        shown: {
            color: 'color-mix(in srgb, var(--chart-5) var(--tour-chart-strength), black)',
            label: 'Shown'
        },
        started: {
            color: 'color-mix(in srgb, var(--chart-3) var(--tour-chart-strength), black)',
            label: 'Started'
        }
    } satisfies Chart.ChartConfig);
    const keys = $derived(prompt ? (['shown', 'completed', 'dismissed'] as const) : (['started', 'completed', 'dismissed'] as const));
    const series = $derived(
        keys.map((key) => ({
            key,
            ...config[key]
        }))
    );
    const total = $derived(tour.shown + tour.started + tour.completed + tour.dismissed);
    let context = $state<ChartState>();
    let keyboardIndex = $state<number>();
    const selectedIndex = $derived(Math.max(0, Math.min(keyboardIndex ?? data.length - 1, data.length - 1)));
    const selectedPeriod = $derived(data[selectedIndex]);
    const keyboardValue = $derived(
        selectedPeriod ? `${formatPeriod(selectedPeriod.date)}. ${keys.map((key) => `${config[key].label}: ${selectedPeriod[key]}`).join('. ')}` : 'No activity'
    );

    function showSelectedPeriod(): void {
        if (selectedPeriod) {
            context?.tooltip.show({
                data: selectedPeriod
            });
        }
    }

    function inspectDate(event: KeyboardEvent): void {
        if (!['ArrowLeft', 'ArrowRight', 'End', 'Home'].includes(event.key)) {
            return;
        }
        event.preventDefault();
        switch (event.key) {
            case 'ArrowLeft':
                keyboardIndex = Math.max(0, selectedIndex - 1);
                break;
            case 'ArrowRight':
                keyboardIndex = Math.min(data.length - 1, selectedIndex + 1);
                break;
            case 'End':
                keyboardIndex = data.length - 1;
                break;
            case 'Home':
                keyboardIndex = 0;
                break;
        }
        showSelectedPeriod();
    }

    function formatPeriod(value: Date): string {
        return formatDateLabel(value, undefined, {
            includeRelative: false,
            month: 'short',
            timeZone: 'UTC'
        });
    }
</script>

<div class="flex flex-col gap-4 [--tour-chart-strength:70%] dark:[--tour-chart-strength:100%]">
    {#if total === 0}
        <Typography.Muted class="flex h-48 items-center justify-center">No recorded activity in this period.</Typography.Muted>
    {:else}
        <ul class="flex flex-wrap gap-x-4 gap-y-1 text-xs" aria-label="Period totals">
            {#each keys as key (key)}
                <li class="flex items-center gap-1.5">
                    <span class="h-0.5 w-3" style={`background: ${config[key].color}`} aria-hidden="true"></span>{config[key].label}
                    <Number value={tour[key]} />
                </li>
            {/each}
        </ul>
        <Chart.Container
            {config}
            class="focus-visible:outline-ring h-48 w-full rounded-sm focus-visible:outline focus-visible:outline-2"
            role="slider"
            tabindex={0}
            aria-label={`Recorded ${prompt ? 'invitation' : 'guide'} activity.`}
            aria-describedby={keyboardHelpId}
            aria-valuemin={0}
            aria-valuemax={Math.max(0, data.length - 1)}
            aria-valuenow={selectedIndex}
            aria-valuetext={keyboardValue}
            onkeydown={inspectDate}
            onfocus={showSelectedPeriod}
            onblur={() => context?.tooltip.hide()}
        >
            <LineChart
                bind:context
                {data}
                x="date"
                xScale={scaleUtc()}
                {series}
                yDomain={[0, null]}
                padding={{
                    bottom: 36,
                    left: 32,
                    right: 32,
                    top: 8
                }}
                axis
                legend={false}
                props={{
                    xAxis: {
                        tickLabelProps: {
                            dy: 20
                        },
                        ticks: 4
                    },
                    yAxis: {
                        tickLabelProps: {
                            dx: -8
                        },
                        ticks: (scale) => scale.ticks?.(3).filter((value: number) => globalThis.Number.isInteger(value))
                    }
                }}
            >
                {#snippet marks({ context })}
                    {#each context.series.visibleSeries as item (item.key)}
                        <Spline seriesKey={item.key} curve={curveLinear} strokeWidth={1.5} />
                        {#if data.length === 1}
                            <Points seriesKey={item.key} r={3} fill={item.color} />
                        {/if}
                    {/each}
                {/snippet}
                {#snippet tooltip()}<Chart.Tooltip
                        role="tooltip"
                        labelFormatter={(value) => (value instanceof Date ? formatPeriod(value) : '')}
                        indicator="line"
                    />{/snippet}
            </LineChart>
        </Chart.Container>
        <div class="sr-only">
            <Typography.P id={keyboardHelpId}
                >Use Left and Right arrows to inspect dates, or Home and End to jump to the first and last date. Dates are UTC.</Typography.P
            >
            <Table.Root aria-label="Guide activity by date">
                <Table.Header
                    ><Table.Row
                        ><Table.Head>Date (UTC)</Table.Head>{#each keys as key (key)}<Table.Head class="text-right">{config[key].label}</Table.Head
                            >{/each}</Table.Row
                    ></Table.Header
                >
                <Table.Body
                    >{#each data as period (period.date_utc)}<Table.Row
                            ><Table.Cell>{formatPeriod(period.date)}</Table.Cell>{#each keys as key (key)}<Table.Cell class="text-right"
                                    ><Number value={period[key]} /></Table.Cell
                                >{/each}</Table.Row
                        >{/each}</Table.Body
                >
            </Table.Root>
        </div>
    {/if}
</div>
