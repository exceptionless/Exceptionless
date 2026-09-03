<script lang="ts">
    import type { ProductTourSummary, ProductTourUsageInterval } from '$generated/api';

    import Number from '$comp/formatters/number.svelte';
    import { Button } from '$comp/ui/button';
    import * as Chart from '$comp/ui/chart';
    import * as Popover from '$comp/ui/popover';
    import * as Table from '$comp/ui/table';
    import { scaleUtc } from 'd3-scale';
    import { curveLinear } from 'd3-shape';
    import { type ChartState, LineChart, Points, Spline } from 'layerchart';

    import { getProductTourActivity } from '../product-tour-usage';

    let { end, interval, start, tour }: { end: string; interval: ProductTourUsageInterval; start?: null | string; tour: ProductTourSummary } = $props();
    const prompt = $derived(tour.kind === 'prompt');
    const keyboardHelpId = $props.id();
    const data = $derived(getProductTourActivity(tour.activity ?? [], interval, start, end));
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
    const commonExit = $derived(
        tour.steps?.filter((step) => step.dismissed > 0).toSorted((a, b) => b.dismissed - a.dismissed || a.step.localeCompare(b.step))[0]
    );
    let context = $state<ChartState>();
    let keyboardIndex = $state<number>();
    const selectedIndex = $derived(Math.max(0, Math.min(keyboardIndex ?? data.length - 1, data.length - 1)));
    const selectedPeriod = $derived(data[selectedIndex]);
    const keyboardValue = $derived(
        selectedPeriod ? `${dateLabel(selectedPeriod.date)}. ${keys.map((key) => `${config[key].label}: ${selectedPeriod[key]}`).join('. ')}` : 'No activity'
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
        keyboardIndex =
            event.key === 'Home'
                ? 0
                : event.key === 'End'
                  ? data.length - 1
                  : Math.max(0, Math.min(data.length - 1, selectedIndex + (event.key === 'ArrowRight' ? 1 : -1)));
        showSelectedPeriod();
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

<div class="flex flex-col gap-4 [--tour-chart-strength:70%] dark:[--tour-chart-strength:100%]">
    {#if total === 0}
        <p class="text-muted-foreground flex h-48 items-center justify-center text-sm">No recorded activity in this period.</p>
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
            aria-label={`${interval === 'day' ? 'Daily' : 'Monthly'} ${prompt ? 'invitation' : 'guide'} activity.`}
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
                    bottom: 20,
                    left: 32,
                    right: 32,
                    top: 8
                }}
                axis
                legend={false}
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
                        <Spline seriesKey={item.key} curve={curveLinear} strokeWidth={1.5} />
                        {#if data.length === 1}
                            <Points seriesKey={item.key} r={3} fill={item.color} />
                        {/if}
                    {/each}
                {/snippet}
                {#snippet tooltip()}<Chart.Tooltip role="tooltip" labelFormatter={dateLabel} indicator="line" />{/snippet}
            </LineChart>
        </Chart.Container>
        {#if commonExit}
            <p class="text-muted-foreground text-xs">Most common exit: {commonExit.step.replaceAll('-', ' ')} · <Number value={commonExit.dismissed} /></p>
        {/if}
        {#if !prompt && (tour.steps?.length || tour.start_sources.length)}
            <Popover.Root>
                <Popover.Trigger>
                    {#snippet child({ props })}
                        <Button {...props} variant="ghost" size="sm" class="text-muted-foreground w-fit">Steps and entry points</Button>
                    {/snippet}
                </Popover.Trigger>
                <Popover.Content align="start" class="max-h-80 overflow-y-auto text-sm">
                    {#if tour.steps?.length}
                        <h3 class="mb-2 font-medium">Steps reached · exits</h3>
                        <ul class="space-y-1" aria-label="Guide steps reached and explicit exits">
                            {#each tour.steps as step (step.step)}
                                <li>
                                    {step.step.replaceAll('-', ' ')}: <Number value={step.reached} /> reached; <Number value={step.dismissed} /> closed here.
                                </li>
                            {/each}
                        </ul>
                    {/if}
                    {#if tour.start_sources.length}
                        <h3 class="mt-3 mb-2 font-medium">Share of starts</h3>
                        <ul class="space-y-1" aria-label="Guide entry points">
                            {#each tour.start_sources as source (source.source)}
                                <li>
                                    {source.source.replaceAll('-', ' ')}: <Number value={source.count} /> ({Math.round((source.count / tour.started) * 100)}%)
                                </li>
                            {/each}
                        </ul>
                    {/if}
                </Popover.Content>
            </Popover.Root>
        {/if}
        <div class="sr-only">
            <p id={keyboardHelpId}>Use Left and Right arrows to inspect dates, or Home and End to jump to the first and last date. Dates are UTC.</p>
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
        </div>
    {/if}
</div>
