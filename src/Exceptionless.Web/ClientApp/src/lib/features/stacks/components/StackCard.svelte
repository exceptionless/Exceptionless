<script lang="ts">
    import type { IFilter } from '$comp/filters/filters.svelte';

    import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
    import DateTime from '$comp/formatters/DateTime.svelte';
    import Number from '$comp/formatters/Number.svelte';
    import Percentage from '$comp/formatters/Percentage.svelte';
    import TimeAgo from '$comp/formatters/TimeAgo.svelte';
    import Muted from '$comp/typography/Muted.svelte';
    import { Badge } from '$comp/ui/badge';
    import * as Card from '$comp/ui/card';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Tooltip from '$comp/ui/tooltip';
    import { getProjectCountQuery, getStackCountQuery } from '$features/events/api.svelte';
    import { DEFAULT_OFFSET } from '$features/shared/api/api.svelte';
    import { getStackQuery } from '$features/stacks/api.svelte';
    import { cardinality, max, min, sum } from '$shared/api/aggregations';
    import IconFirstOccurrence from '~icons/mdi/arrow-left-circle';
    import IconLastOccurrence from '~icons/mdi/arrow-right-circle';
    import IconCalendar from '~icons/mdi/calendar';
    import IconClock from '~icons/mdi/clock';
    import IconFilter from '~icons/mdi/filter';
    import IconUsers from '~icons/mdi/users';

    import StackOptionsDropdownMenu from './StackOptionsDropdownMenu.svelte';
    import StackReferences from './StackReferences.svelte';
    import StackStatusDropdownMenu from './StackStatusDropdownMenu.svelte';

    interface Props {
        changed: (filter: IFilter) => void;
        id: string | undefined;
    }

    let { changed, id }: Props = $props();

    const stackResponse = getStackQuery({
        route: {
            get id() {
                return id;
            }
        }
    });

    const projectCountResponse = getProjectCountQuery({
        params: {
            aggregations: 'cardinality:user'
        },
        route: {
            get projectId() {
                return stackResponse.data?.project_id;
            }
        }
    });

    // TODO: Add stack charts for Occurrences, Average Value, Value Sum
    const stackCountResponse = getStackCountQuery({
        params: {
            aggregations: `date:(date${DEFAULT_OFFSET ? '^' + DEFAULT_OFFSET : ''} cardinality:user sum:count~1) min:date max:date cardinality:user sum:count~1`
        },
        route: {
            get stackId() {
                return id;
            }
        }
    });

    const stack = $derived(stackResponse.data!);
    const eventOccurrences = $derived(sum(stackCountResponse?.data?.aggregations, 'sum_count')?.value ?? 0);
    const totalOccurrences = $derived(stack && stack.total_occurrences > eventOccurrences ? stack.total_occurrences : eventOccurrences);
    const userCount = $derived(sum(stackCountResponse?.data?.aggregations, 'cardinality_user')?.value ?? 0);
    const totalUserCount = $derived(cardinality(projectCountResponse?.data?.aggregations, 'cardinality_user')?.value ?? 0);
    const firstOccurrence = $derived(min<string>(stackCountResponse?.data?.aggregations, 'min_date')?.value ?? stack?.first_occurrence);
    const lastOccurrence = $derived(max<string>(stackCountResponse?.data?.aggregations, 'max_date')?.value ?? stack?.last_occurrence);
</script>

{#if stackResponse.isSuccess}
    <Card.Root>
        <Card.Header>
            <Card.Title class="flex flex-row items-center justify-between text-lg font-semibold">
                <span class="mb-2 flex flex-col lg:mb-0">
                    <div class="flex items-center">
                        <ClickableStringFilter {changed} class="mr-2" term="stack" value={stack.id} />
                        <span class="truncate">{stack.title}</span>
                    </div>
                </span>
                <div class="flex items-center space-x-2">
                    <StackStatusDropdownMenu {stack} />
                    <StackOptionsDropdownMenu {stack} />
                </div>
            </Card.Title>
        </Card.Header>
        <Card.Content class="space-y-4 pt-2">
            <div class="grid grid-cols-2 gap-4 lg:grid-cols-4">
                <Tooltip.Root>
                    <Tooltip.Trigger class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <IconCalendar class="mb-1 size-6 text-primary" />
                        <span class="text-lg font-bold"><Number value={totalOccurrences} /></span>
                        <Muted>Total Events</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">
                        <Number value={totalOccurrences} /> All Time
                    </Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <IconUsers class="mb-1 size-6 text-primary" />
                        <span class="text-lg font-bold"><Percentage percent={(userCount / totalUserCount) * 100.0} /></span>
                        <Muted>Users Affected</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom"><Number value={userCount} /> of <Number value={totalUserCount} /> Users Affected</Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <IconFirstOccurrence class="mb-1 size-6 text-muted-foreground" />
                        <span class="text-lg font-bold"><TimeAgo value={firstOccurrence} /></span>
                        <Muted>First</Muted>
                    </Tooltip.Trigger>
                    <Tooltip.Content side="bottom">
                        First Occurred On <DateTime value={stack.first_occurrence} />
                    </Tooltip.Content>
                </Tooltip.Root>
                <Tooltip.Root>
                    <Tooltip.Trigger class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <IconLastOccurrence class="mb-1 size-6 text-muted-foreground" />
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
                        <IconCalendar class="size-4 text-green-500" />
                        <span>
                            Fixed {stack.fixed_in_version && `in ${stack.fixed_in_version}`} on <DateTime value={stack.date_fixed} />
                        </span>
                    </div>
                {/if}

                {#if stack.status === 'snoozed' && stack.snooze_until_utc}
                    <div class="flex items-center gap-2 text-sm">
                        <IconClock class="size-4 text-blue-500" />
                        <span>Snoozed until <DateTime value={stack.snooze_until_utc} /></span>
                    </div>
                {/if}
            </div>

            {#if stack.tags && stack.tags.length > 0}
                <div class="flex flex-wrap gap-2">
                    {#each stack.tags as tag (tag)}
                        <Badge color="dark"
                            ><ClickableStringFilter {changed} class="mr-1" term="tag" value={tag}
                                ><IconFilter class="text-muted-foreground text-opacity-80 hover:text-secondary" /></ClickableStringFilter
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
                {#each Array(4)}
                    <div class="flex flex-col items-center rounded-lg bg-muted p-2">
                        <Skeleton class="mb-1 size-6" />
                        <Skeleton class="mb-1 h-[28px] w-[60px]" />
                        <Skeleton class="h-[24px] w-[80px]" />
                    </div>
                {/each}
            </div>
        </Card.Content>
    </Card.Root>
{/if}
