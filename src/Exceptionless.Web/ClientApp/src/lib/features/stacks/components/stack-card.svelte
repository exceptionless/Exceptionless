<script lang="ts">
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { type IFilter } from '$comp/faceted-filter';
    import DateTime from '$comp/formatters/date-time.svelte';
    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import Muted from '$comp/typography/muted.svelte';
    import * as ButtonGroup from '$comp/ui/button-group';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Tooltip from '$comp/ui/tooltip';
    import { getProjectCountQuery, getStackCountQuery } from '$features/events/api.svelte';
    import EventsStackChart, { type EventsStackChartPoint } from '$features/events/components/events-stack-chart.svelte';
    import * as EventsFacetedFilter from '$features/events/components/filters';
    import { StringFilter } from '$features/events/components/filters';
    import * as agg from '$features/shared/api/aggregations';
    import { fillDateSeries } from '$features/shared/utils/charts';
    import { getStackQuery } from '$features/stacks/api.svelte';
    import { DEFAULT_OFFSET } from '$shared/api/api.svelte';
    import FirstOccurrence from '@lucide/svelte/icons/arrow-left-circle';
    import LastOccurrence from '@lucide/svelte/icons/arrow-right-circle';
    import Calendar from '@lucide/svelte/icons/calendar';
    import Clock from '@lucide/svelte/icons/clock';
    import Filter from '@lucide/svelte/icons/filter';
    import Info from '@lucide/svelte/icons/info';
    import Users from '@lucide/svelte/icons/users';

    import StackLogLevel from './stack-log-level.svelte';
    import StackOptionsDropdownMenu from './stack-options-dropdown-menu.svelte';
    import StackReferences from './stack-references.svelte';
    import StackStatusDropdownMenu from './stack-status-dropdown-menu.svelte';

    interface Props {
        filterChanged: (filter: IFilter) => void;
        id: string | undefined;
        onDeleted?: () => void;
        onError?: (problem: ProblemDetails) => void;
    }

    let { filterChanged, id, onDeleted, onError }: Props = $props();
    let handledErrorForStackId = $state<string>();

    const stackQuery = getStackQuery({
        route: {
            get id() {
                return id;
            }
        }
    });

    const projectCountQuery = getProjectCountQuery({
        params: {
            aggregations: 'cardinality:user'
        },
        route: {
            get projectId() {
                return stackQuery.data?.project_id;
            }
        }
    });

    // TODO: Log Level
    const stack = $derived(stackQuery.data!);

    // TODO: Add stack charts for Occurrences, Average Value, Value Sum
    const stackCountQuery = getStackCountQuery({
        params: {
            aggregations: `date:(date${DEFAULT_OFFSET ? '^' + DEFAULT_OFFSET : ''} cardinality:user sum:count~1) min:date max:date cardinality:user sum:count~1`,
            time: '[now-7d TO now]'
        },
        route: {
            get stackId() {
                return id;
            }
        }
    });

    const eventOccurrences = $derived(agg.sum(stackCountQuery?.data?.aggregations, 'sum_count')?.value ?? 0);
    const totalOccurrences = $derived(stack && stack.total_occurrences > eventOccurrences ? stack.total_occurrences : eventOccurrences);
    const userCount = $derived(agg.sum(stackCountQuery?.data?.aggregations, 'cardinality_user')?.value ?? 0);
    const totalUserCount = $derived(agg.cardinality(projectCountQuery?.data?.aggregations, 'cardinality_user')?.value ?? 0);
    const firstOccurrence = $derived(agg.min<string>(stackCountQuery?.data?.aggregations, 'min_date')?.value ?? stack?.first_occurrence);
    const lastOccurrence = $derived(agg.max<string>(stackCountQuery?.data?.aggregations, 'max_date')?.value ?? stack?.last_occurrence);

    const metricCardClass = 'relative justify-between gap-1! bg-muted/50 py-2! ring-1 ring-border';
    const metricHeaderClass = 'flex flex-row items-center justify-between gap-1.5 px-3 pb-0';
    const metricTitleClass = 'min-w-0 truncate text-xs font-semibold text-muted-foreground';
    const metricIconClass = 'size-3.5 shrink-0 text-muted-foreground';
    const metricValueClass = 'truncate text-lg font-bold tabular-nums sm:text-xl';

    const chartData = $derived(() => {
        const now = new Date();
        const SEVEN_DAYS_IN_MS = 7 * 24 * 60 * 60 * 1000;
        const sevenDaysAgo = new Date(now.getTime() - SEVEN_DAYS_IN_MS);
        const buildZeroFilledSeries = () =>
            fillDateSeries(
                sevenDaysAgo,
                now,
                (date: Date) =>
                    ({
                        date,
                        occurrences: 0
                    }) as EventsStackChartPoint
            );

        const dateHistogramBuckets = agg.dateHistogram(stackCountQuery.data?.aggregations, 'date_date')?.buckets ?? [];
        const recentBuckets = dateHistogramBuckets
            .map(
                (bucket) =>
                    ({
                        date: new Date(bucket.key),
                        occurrences: agg.sum(bucket.aggregations, 'sum_count')?.value ?? 0
                    }) as EventsStackChartPoint
            )
            .filter((bucket) => bucket.date >= sevenDaysAgo);

        if (recentBuckets.length === 0) {
            return buildZeroFilledSeries();
        }

        return recentBuckets;
    });

    $effect(() => {
        if (!stackQuery.isError || handledErrorForStackId === id) {
            return;
        }

        handledErrorForStackId = id;
        onError?.(stackQuery.error);
    });
</script>

{#if stackQuery.isSuccess}
    <Card.Root
        class="bg-background relative overflow-hidden ring-[color-mix(in_oklab,var(--chart-1)_42%,transparent)] before:absolute before:inset-x-0 before:top-0 before:h-1 before:bg-[linear-gradient(90deg,var(--chart-1),var(--chart-2))] before:content-['']"
    >
        <Card.Header>
            <Card.Title class="flex flex-row items-center justify-between text-lg font-semibold">
                <div class="mb-2 flex w-0 min-w-0 flex-1 flex-col lg:mb-0">
                    <div class="flex min-w-0 items-center">
                        <span class="block max-w-full min-w-0 truncate" title={stack.title}>{stack.title}</span>
                    </div>
                </div>
                <div class="ml-2 flex shrink-0 items-center gap-2">
                    <StackLogLevel {stack} />
                    <ButtonGroup.Root>
                        <StackStatusDropdownMenu {stack} />
                        <StackOptionsDropdownMenu {onDeleted} {stack} />
                    </ButtonGroup.Root>
                </div>
            </Card.Title>
        </Card.Header>
        <Card.Content class="space-y-2">
            <div class="grid grid-cols-2 gap-2 lg:grid-cols-4">
                <Card.Root size="sm" class={metricCardClass}>
                    <Card.Header class={metricHeaderClass}>
                        <div class="flex min-w-0 items-center gap-1.5">
                            <Calendar aria-hidden="true" class={metricIconClass} />
                            <Card.Title class={metricTitleClass}>Total Events</Card.Title>
                        </div>
                        <Tooltip.Root>
                            <Tooltip.Trigger
                                class="text-muted-foreground hover:text-foreground focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2"
                                aria-label="About total events"
                            >
                                <Info aria-hidden="true" class="size-3.5" />
                            </Tooltip.Trigger>
                            <Tooltip.Content sideOffset={6}><Number value={totalOccurrences} /> All Time</Tooltip.Content>
                        </Tooltip.Root>
                    </Card.Header>
                    <Card.Content class="px-3">
                        <button class={metricValueClass} onclick={() => filterChanged(new StringFilter('stack', stack.id))} type="button">
                            <Number value={totalOccurrences} />
                        </button>
                    </Card.Content>
                </Card.Root>

                <Card.Root size="sm" class={metricCardClass}>
                    <Card.Header class={metricHeaderClass}>
                        <div class="flex min-w-0 items-center gap-1.5">
                            <Users aria-hidden="true" class={metricIconClass} />
                            <Card.Title class={metricTitleClass}>Users Affected</Card.Title>
                        </div>
                        <Tooltip.Root>
                            <Tooltip.Trigger
                                class="text-muted-foreground hover:text-foreground focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2"
                                aria-label="About users affected"
                            >
                                <Info aria-hidden="true" class="size-3.5" />
                            </Tooltip.Trigger>
                            <Tooltip.Content sideOffset={6}><Number value={userCount} /> of <Number value={totalUserCount} /> Users Affected</Tooltip.Content>
                        </Tooltip.Root>
                    </Card.Header>
                    <Card.Content class="px-3">
                        <div class={metricValueClass}>
                            <Percentage percent={(userCount / totalUserCount) * 100.0} />
                        </div>
                    </Card.Content>
                </Card.Root>

                <Card.Root size="sm" class={metricCardClass}>
                    <Card.Header class={metricHeaderClass}>
                        <div class="flex min-w-0 items-center gap-1.5">
                            <FirstOccurrence aria-hidden="true" class={metricIconClass} />
                            <Card.Title class={metricTitleClass}>First</Card.Title>
                        </div>
                        <Tooltip.Root>
                            <Tooltip.Trigger
                                class="text-muted-foreground hover:text-foreground focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2"
                                aria-label="About first occurrence"
                            >
                                <Info aria-hidden="true" class="size-3.5" />
                            </Tooltip.Trigger>
                            <Tooltip.Content sideOffset={6}>First Occurred On <DateTime value={stack.first_occurrence} /></Tooltip.Content>
                        </Tooltip.Root>
                    </Card.Header>
                    <Card.Content class="px-3">
                        <div class={metricValueClass}>
                            <TimeAgo value={firstOccurrence} />
                        </div>
                    </Card.Content>
                </Card.Root>

                <Card.Root size="sm" class={metricCardClass}>
                    <Card.Header class={metricHeaderClass}>
                        <div class="flex min-w-0 items-center gap-1.5">
                            <LastOccurrence aria-hidden="true" class={metricIconClass} />
                            <Card.Title class={metricTitleClass}>Last</Card.Title>
                        </div>
                        <Tooltip.Root>
                            <Tooltip.Trigger
                                class="text-muted-foreground hover:text-foreground focus-visible:ring-ring rounded-sm outline-none focus-visible:ring-2"
                                aria-label="About last occurrence"
                            >
                                <Info aria-hidden="true" class="size-3.5" />
                            </Tooltip.Trigger>
                            <Tooltip.Content sideOffset={6}>Last Occurred On <DateTime value={lastOccurrence} /></Tooltip.Content>
                        </Tooltip.Root>
                    </Card.Header>
                    <Card.Content class="px-3">
                        <div class={metricValueClass}>
                            <TimeAgo value={lastOccurrence} />
                        </div>
                    </Card.Content>
                </Card.Root>
            </div>

            <div class="flex justify-end">
                <Muted class="text-xs uppercase">Last 7 days</Muted>
            </div>

            <EventsStackChart class="h-12 w-full" data={chartData()} isLoading={stackCountQuery.isLoading} />

            <div class="grid grid-cols-1 gap-x-4 lg:grid-cols-2">
                {#if (stack.status === 'fixed' || stack.status === 'regressed') && stack.date_fixed}
                    <div class="flex items-center gap-2 text-sm">
                        <Calendar class="size-4 text-green-500" />
                        <span>
                            Fixed {stack.fixed_in_version && `in ${stack.fixed_in_version}`} on <DateTime value={stack.date_fixed} />
                        </span>
                    </div>
                {/if}

                {#if stack.status === 'snoozed' && stack.snooze_until_utc}
                    <div class="flex items-center gap-2 text-sm">
                        <Clock class="size-4 text-blue-500" />
                        <span>Snoozed until <DateTime value={stack.snooze_until_utc} /></span>
                    </div>
                {/if}
            </div>

            {#if stack.tags && stack.tags.length > 0}
                <div class="flex flex-wrap gap-2">
                    {#each stack.tags as tag (tag)}
                        <EventsFacetedFilter.TagTrigger changed={filterChanged} value={[tag]}>
                            <Filter class="size-3" />
                            {tag}
                        </EventsFacetedFilter.TagTrigger>
                    {/each}
                </div>
            {/if}

            <StackReferences {stack} />
        </Card.Content>
    </Card.Root>
{:else}
    <Card.Root class="bg-background">
        <Card.Header>
            <Card.Title class="flex flex-row items-center justify-between text-lg font-semibold">
                <span class="mb-2 flex flex-col lg:mb-0">
                    <div class="flex items-center gap-2">
                        <Skeleton class="h-6 w-8" />
                        <Skeleton class="h-6 w-48" />
                    </div>
                </span>
                <div class="flex items-center space-x-2">
                    <Skeleton class="h-9 w-36" />
                    <Skeleton class="h-9 w-8" />
                </div>
            </Card.Title>
        </Card.Header>
        <Card.Content class="space-y-2">
            <div class="grid grid-cols-2 gap-2 lg:grid-cols-4">
                {#each { length: 4 } as name, index (`${name}-${index}`)}
                    <Card.Root size="sm" class={metricCardClass}>
                        <Card.Header class={metricHeaderClass}>
                            <Skeleton class="h-3.5 w-20" />
                        </Card.Header>
                        <Card.Content class="px-3">
                            <Skeleton class="h-5 w-12" />
                        </Card.Content>
                    </Card.Root>
                {/each}
            </div>

            <div class="flex justify-end">
                <Muted class="text-xs uppercase">Last 7 days</Muted>
            </div>

            <Skeleton class="h-12 w-full rounded-md" />
        </Card.Content>
    </Card.Root>
{/if}
