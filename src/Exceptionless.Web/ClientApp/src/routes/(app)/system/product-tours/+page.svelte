<script lang="ts">
    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import * as Card from '$comp/ui/card';
    import { Input } from '$comp/ui/input';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { getAdminProductTourUsageQuery } from '$features/admin/api.svelte';
    import { getUtcMonthKey } from '$features/admin/assistant-usage';

    const currentMonth = getUtcMonthKey();
    let selectedMonth = $state(currentMonth);
    const usageQuery = getAdminProductTourUsageQuery(() => selectedMonth);
    const usage = $derived(usageQuery.data);

    function title(value: string): string {
        return value
            .split('-')
            .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
            .join(' ');
    }
</script>

<div class="space-y-6">
    <div class="flex flex-wrap items-end justify-between gap-4">
        <Muted>Tour starts, outcomes, and recent user activity recorded by Exceptionless Feature Usage events</Muted>
        <label class="flex flex-col gap-1 text-sm font-medium">
            Month
            <Input class="w-40" type="month" max={currentMonth} bind:value={selectedMonth} />
        </label>
    </div>

    {#if usageQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load guided-tour usage. Please try again.</p>
            </Card.Content>
        </Card.Root>
    {:else}
        <Card.Root>
            <Card.Header>
                <Card.Title>Tour Outcomes</Card.Title>
                <Card.Description>
                    Tour rates use starts as the denominator. Welcome and announcement decision rates use shown prompts because they do not have a separate
                    start event.
                </Card.Description>
            </Card.Header>
            <Card.Content class="px-0">
                {#if usageQuery.isPending}
                    <div class="space-y-3 px-4 pb-2">
                        {#each [0, 1, 2, 3, 4] as row (row)}
                            <Skeleton class="h-12 w-full rounded" aria-label={`Loading tour ${row + 1}`} />
                        {/each}
                    </div>
                {:else if usage?.tours.length === 0}
                    <p class="text-muted-foreground px-4 py-10 text-center text-sm">No guided-tour activity was recorded for this month.</p>
                {:else}
                    <Table.Root aria-label="Tour outcomes" class="min-w-4xl">
                        <Table.Header>
                            <Table.Row>
                                <Table.Head class="pl-4">Tour</Table.Head>
                                <Table.Head class="text-right">Shown</Table.Head>
                                <Table.Head class="text-right">Started</Table.Head>
                                <Table.Head class="text-right">Users</Table.Head>
                                <Table.Head class="text-right">Completed</Table.Head>
                                <Table.Head class="text-right">Dismissed</Table.Head>
                                <Table.Head>Last Run</Table.Head>
                            </Table.Row>
                        </Table.Header>
                        <Table.Body>
                            {#each usage?.tours ?? [] as tour (tour.name)}
                                <Table.Row>
                                    <Table.Cell class="pl-4 font-medium">{title(tour.name)}</Table.Cell>
                                    <Table.Cell class="text-right"><Number value={tour.shown} /></Table.Cell>
                                    <Table.Cell class="text-right"><Number value={tour.started} /></Table.Cell>
                                    <Table.Cell class="text-right"><Number value={tour.unique_users} /></Table.Cell>
                                    <Table.Cell class="text-right">
                                        <Number value={tour.completed} />
                                        <span class="text-muted-foreground ml-1 text-xs">
                                            ({#if tour.completion_rate == null}—{:else}<Percentage percent={tour.completion_rate * 100} />{/if})
                                        </span>
                                    </Table.Cell>
                                    <Table.Cell class="text-right">
                                        <Number value={tour.dismissed} />
                                        <span class="text-muted-foreground ml-1 text-xs">
                                            ({#if tour.dismissal_rate == null}—{:else}<Percentage percent={tour.dismissal_rate * 100} />{/if})
                                        </span>
                                    </Table.Cell>
                                    <Table.Cell>
                                        {#if tour.last_run_utc}
                                            <TimeAgo value={tour.last_run_utc} />
                                        {:else}
                                            <span class="text-muted-foreground">Never</span>
                                        {/if}
                                    </Table.Cell>
                                </Table.Row>
                            {/each}
                        </Table.Body>
                    </Table.Root>
                {/if}
            </Card.Content>
        </Card.Root>

        <Card.Root>
            <Card.Header>
                <Card.Title>Recent Activity</Card.Title>
                <Card.Description>Latest identified-user tour events for investigating support and onboarding patterns.</Card.Description>
            </Card.Header>
            <Card.Content class="px-0">
                {#if usage?.recent_activity.length === 0}
                    <p class="text-muted-foreground px-4 py-10 text-center text-sm">No recent tour activity was recorded.</p>
                {:else}
                    <Table.Root aria-label="Recent guided-tour activity" class="min-w-3xl">
                        <Table.Header>
                            <Table.Row>
                                <Table.Head class="pl-4">User</Table.Head>
                                <Table.Head>Tour</Table.Head>
                                <Table.Head>Event</Table.Head>
                                <Table.Head>Source</Table.Head>
                                <Table.Head>When</Table.Head>
                            </Table.Row>
                        </Table.Header>
                        <Table.Body>
                            {#each usage?.recent_activity ?? [] as activity (activity)}
                                <Table.Row>
                                    <Table.Cell class="pl-4">
                                        <div class="font-medium">{activity.user_name || activity.user_identity || 'Unknown user'}</div>
                                        {#if activity.user_name && activity.user_identity}
                                            <div class="text-muted-foreground font-mono text-xs">{activity.user_identity}</div>
                                        {/if}
                                    </Table.Cell>
                                    <Table.Cell>{title(activity.tour_name)} <span class="text-muted-foreground">v{activity.version}</span></Table.Cell>
                                    <Table.Cell><Badge variant="secondary">{title(activity.event)}</Badge></Table.Cell>
                                    <Table.Cell>{title(activity.launch_source)}</Table.Cell>
                                    <Table.Cell><TimeAgo value={activity.date_utc} /></Table.Cell>
                                </Table.Row>
                            {/each}
                        </Table.Body>
                    </Table.Root>
                {/if}
            </Card.Content>
        </Card.Root>
    {/if}
</div>
