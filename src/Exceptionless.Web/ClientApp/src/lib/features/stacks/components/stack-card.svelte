<script lang="ts">
    import { type IFilter } from '$comp/faceted-filter';
    import DateTime from '$comp/formatters/date-time.svelte';
    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import Muted from '$comp/typography/muted.svelte';
    import { Badge } from '$comp/ui/badge';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Tooltip from '$comp/ui/tooltip';
    import { getProjectCountQuery, getStackCountQuery } from '$features/events/api.svelte';
    import * as EventsFacetedFilter from '$features/events/components/filters';
    import { StringFilter } from '$features/events/components/filters';
    import { getStackQuery } from '$features/stacks/api.svelte';
    import { cardinality, max, min, sum } from '$shared/api/aggregations';
    import { DEFAULT_OFFSET } from '$shared/api/api.svelte';
    import FirstOccurrence from '@lucide/svelte/icons/arrow-left-circle';
    import LastOccurrence from '@lucide/svelte/icons/arrow-right-circle';
    import Calendar from '@lucide/svelte/icons/calendar';
    import Clock from '@lucide/svelte/icons/clock';
    import Filter from '@lucide/svelte/icons/filter';
    import Users from '@lucide/svelte/icons/users';

    import StackOptionsDropdownMenu from './stack-options-dropdown-menu.svelte';
    import StackReferences from './stack-references.svelte';
    import StackStatusDropdownMenu from './stack-status-dropdown-menu.svelte';

    interface Props {
        filterChanged: (filter: IFilter) => void;
        id: string | undefined;
    }

    let { filterChanged, id }: Props = $props();

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

    // TODO: Add stack charts for Occurrences, Average Value, Value Sum
    const stackCountQuery = getStackCountQuery({
        params: {
            aggregations: `date:(date${DEFAULT_OFFSET ? '^' + DEFAULT_OFFSET : ''} cardinality:user sum:count~1) min:date max:date cardinality:user sum:count~1`
        },
        route: {
            get stackId() {
                return id;
            }
        }
    });

    // TODO: Log Level
    const stack = $derived(stackQuery.data!);
    const eventOccurrences = $derived(sum(stackCountQuery?.data?.aggregations, 'sum_count')?.value ?? 0);
    const totalOccurrences = $derived(stack && stack.total_occurrences > eventOccurrences ? stack.total_occurrences : eventOccurrences);
    const userCount = $derived(sum(stackCountQuery?.data?.aggregations, 'cardinality_user')?.value ?? 0);
    const totalUserCount = $derived(cardinality(projectCountQuery?.data?.aggregations, 'cardinality_user')?.value ?? 0);
    const firstOccurrence = $derived(min<string>(stackCountQuery?.data?.aggregations, 'min_date')?.value ?? stack?.first_occurrence);
    const lastOccurrence = $derived(max<string>(stackCountQuery?.data?.aggregations, 'max_date')?.value ?? stack?.last_occurrence);
</script>

{#if stackQuery.isSuccess}
    <Card.Root>
        <Card.Header>
            <Card.Title class="flex flex-row items-center justify-between text-lg font-semibold">
                <div class="mb-2 flex w-0 min-w-0 flex-1 flex-col lg:mb-0">
                    <div class="flex min-w-0 items-center">
                        <EventsFacetedFilter.StringTrigger changed={filterChanged} class="mr-2 shrink-0" term="stack" value={stack.id} />
                        <span class="block max-w-full min-w-0 truncate" title={stack.title}>{stack.title}</span>
                    </div>
                </div>
                <div class="ml-2 flex shrink-0 items-center space-x-2">
                    <StackStatusDropdownMenu {stack} />
                    <StackOptionsDropdownMenu {stack} />
                </div>
            </Card.Title>
        </Card.Header>
        <Card.Content class="space-y-4 pt-2">
            <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
                <Tooltip.Root>
                    <Tooltip.Trigger
                        class="bg-muted flex flex-col items-center rounded-lg p-2"
                        onclick={() => filterChanged(new StringFilter('stack', stack.id))}
                    >
                        <Calendar class="text-primary mb-1 size-6" />
                        <span class="text-lg font-bold"><Number value={totalOccurrences} /></span>
                        <Muted>Total Events</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">
                        <Number value={totalOccurrences} /> All Time
                    </Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="bg-muted flex flex-col items-center rounded-lg p-2">
                        <Users class="text-primary mb-1 size-6" />
                        <span class="text-lg font-bold"><Percentage percent={(userCount / totalUserCount) * 100.0} /></span>
                        <Muted>Users Affected</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom"><Number value={userCount} /> of <Number value={totalUserCount} /> Users Affected</Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="bg-muted flex flex-col items-center rounded-lg p-2">
                        <FirstOccurrence class="text-muted-foreground mb-1 size-6" />
                        <span class="text-lg font-bold"><TimeAgo value={firstOccurrence} /></span>
                        <Muted>First</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">
                        First Occurred On <DateTime value={stack.first_occurrence} />
                    </Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="bg-muted flex flex-col items-center rounded-lg p-2">
                        <LastOccurrence class="text-muted-foreground mb-1 size-6" />
                        <span class="text-lg font-bold"><TimeAgo value={lastOccurrence} /></span>
                        <Muted>Last</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">
                        Last Occurred On <DateTime value={lastOccurrence} />
                    </Tooltip.Content>
                </Tooltip.Root>
            </div>

            <!-- Line Chart -->

            <div class="grid grid-cols-1 gap-4 lg:grid-cols-2">
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
                        <Badge color="dark"
                            ><EventsFacetedFilter.TagTrigger changed={filterChanged} class="mr-1" value={[tag]}
                                ><Filter class="text-muted-foreground text-opacity-50 hover:text-secondary size-5" /></EventsFacetedFilter.TagTrigger
                            >{tag}</Badge
                        >
                    {/each}
                </div>
            {/if}

            <StackReferences {stack} />
        </Card.Content>
    </Card.Root>
{:else}
    <Card.Root>
        <Card.Header>
            <Card.Title class="flex flex-row items-center justify-between text-lg font-semibold">
                <span class="mb-2 flex flex-col lg:mb-0">
                    <div class="flex items-center gap-2">
                        <Skeleton class="h-[26px] w-[32px]" />
                        <Skeleton class="h-[26px] w-[200px]" />
                    </div>
                </span>
                <div class="flex items-center space-x-2">
                    <Skeleton class="h-[36px] w-[135px]" />
                    <Skeleton class="h-[36px] w-[32px]" />
                </div>
            </Card.Title>
        </Card.Header>
        <Card.Content class="space-y-4 pt-2">
            <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
                {#each { length: 4 } as name, index (`${name}-${index}`)}
                    <div class="bg-muted flex flex-col items-center rounded-lg p-2">
                        <Skeleton class="mb-1 size-6" />
                        <Skeleton class="mb-1 h-[28px] w-[60px]" />
                        <Skeleton class="h-[24px] w-[80px]" />
                    </div>
                {/each}
            </div>
        </Card.Content>
    </Card.Root>
{/if}
