<script lang="ts">
    import type { ProductTourSummary } from '$generated/api';

    import DateTime from '$comp/formatters/date-time.svelte';
    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as ButtonGroup from '$comp/ui/button-group';
    import * as Card from '$comp/ui/card';
    import { Input } from '$comp/ui/input';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { getAdminProductTourUsageQuery } from '$features/admin/api.svelte';
    import { getUtcMonthKey } from '$features/admin/assistant-usage';
    import { getOutcomeShare, getRate, getStartSourceShare, type ProductTourUsageRange } from '$features/admin/product-tour-usage';

    const currentMonth = getUtcMonthKey();
    let selectedMonth = $state(currentMonth);
    let range = $state<ProductTourUsageRange>({
        kind: 'month',
        month: currentMonth
    });
    const usageQuery = getAdminProductTourUsageQuery(() => range);
    const usage = $derived(usageQuery.data);

    function title(value: string): string {
        return value
            .split('-')
            .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
            .join(' ');
    }

    function setMonth(month: string): void {
        selectedMonth = month;
        range = {
            kind: 'month',
            month
        };
    }

    function setHistory(): void {
        range = {
            kind: 'history'
        };
    }

    function formatSource(source: string): string {
        return source === 'welcome' ? 'Welcome' : title(source);
    }

    function promptEngagement(tour: ProductTourSummary): null | number {
        return getRate(tour.completed, tour.shown);
    }

    function promptDismissal(tour: ProductTourSummary): null | number {
        return getRate(tour.dismissed, tour.shown);
    }

    function promptStart(tour: ProductTourSummary): null | number {
        return getRate(tour.started, tour.shown);
    }
</script>

<div class="space-y-6">
    <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
            <Muted>Feature Usage events for guided-tour prompts and guides. Counts are events, not unique users.</Muted>
            {#if usage}
                <p class="text-muted-foreground mt-1 text-xs">
                    Bounds: {#if usage.utc_start}<DateTime value={usage.utc_start} />{:else}available history{/if} – <DateTime value={usage.utc_end} />
                </p>
            {/if}
        </div>
        <div class="flex items-end gap-2">
            <ButtonGroup.Root aria-label="Usage period">
                <Button variant={range.kind === 'history' ? 'secondary' : 'outline'} aria-pressed={range.kind === 'history'} onclick={setHistory}
                    >Available history</Button
                >
                <Button variant={range.kind === 'month' ? 'secondary' : 'outline'} aria-pressed={range.kind === 'month'} onclick={() => setMonth(selectedMonth)}
                    >Month</Button
                >
            </ButtonGroup.Root>
            {#if range.kind === 'month'}
                <label class="flex flex-col gap-1 text-sm font-medium">
                    Month
                    <Input class="w-40" type="month" max={currentMonth} value={selectedMonth} onchange={(event) => setMonth(event.currentTarget.value)} />
                </label>
            {/if}
        </div>
    </div>

    {#if usageQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load guided-tour usage. Please try again.</p>
            </Card.Content>
        </Card.Root>
    {:else}
        {#if usageQuery.isPending}
            <Card.Root>
                <Card.Header>
                    <Card.Title>Guided-tour usage</Card.Title>
                </Card.Header>
                <Card.Content class="space-y-3">
                    {#each [0, 1, 2, 3] as row (row)}
                        <Skeleton class="h-16 w-full rounded" aria-label={`Loading tour ${row + 1}`} />
                    {/each}
                </Card.Content>
            </Card.Root>
        {:else if usage?.tours.length === 0}
            <Card.Root>
                <Card.Content class="text-muted-foreground py-10 text-center text-sm">No guided-tour usage events were recorded for this range.</Card.Content>
            </Card.Root>
        {:else}
            {@const prompts = usage?.tours.filter((tour) => tour.kind === 'prompt') ?? []}
            {@const guides = usage?.tours.filter((tour) => tour.kind === 'guide') ?? []}

            {#if prompts.length > 0}
                <Card.Root>
                    <Card.Header>
                        <Card.Title>Prompts</Card.Title>
                        <Card.Description>Prompt impressions and responses. Percentages use shown impressions as the denominator.</Card.Description>
                    </Card.Header>
                    <Card.Content class="px-0">
                        <div class="hidden overflow-x-auto md:block">
                            <Table.Root aria-label="Prompt usage" class="min-w-3xl">
                                <Table.Header>
                                    <Table.Row>
                                        <Table.Head class="pl-4">Prompt</Table.Head>
                                        <Table.Head class="text-right">Shown</Table.Head>
                                        <Table.Head class="text-right">Started</Table.Head>
                                        <Table.Head class="text-right">Engaged</Table.Head>
                                        <Table.Head class="text-right">Dismissed</Table.Head>
                                        <Table.Head>Last event</Table.Head>
                                    </Table.Row>
                                </Table.Header>
                                <Table.Body>
                                    {#each prompts as tour (`${tour.name}:${tour.version}`)}
                                        <Table.Row>
                                            <Table.Cell class="pl-4 font-medium"
                                                >{title(tour.name)} <span class="text-muted-foreground font-normal">v{tour.version}</span></Table.Cell
                                            >
                                            <Table.Cell class="text-right"><Number value={tour.shown} /></Table.Cell>
                                            {@render RateCell(tour.started, promptStart(tour))}
                                            {@render RateCell(tour.completed, promptEngagement(tour))}
                                            {@render RateCell(tour.dismissed, promptDismissal(tour))}
                                            {@render LastRun(tour.last_run_utc)}
                                        </Table.Row>
                                    {/each}
                                </Table.Body>
                            </Table.Root>
                        </div>
                        <div class="grid gap-3 px-4 md:hidden">
                            {#each prompts as tour (`${tour.name}:${tour.version}`)}
                                {@render PromptCard(tour)}
                            {/each}
                        </div>
                    </Card.Content>
                </Card.Root>
            {/if}

            {#if guides.length > 0}
                <Card.Root>
                    <Card.Header>
                        <Card.Title>Guides</Card.Title>
                        <Card.Description>Guide outcomes use completed plus dismissed events. Entry-point percentages use starts.</Card.Description>
                    </Card.Header>
                    <Card.Content class="px-0">
                        <div class="hidden overflow-x-auto md:block">
                            <Table.Root aria-label="Guide usage" class="min-w-4xl">
                                <Table.Header>
                                    <Table.Row>
                                        <Table.Head class="pl-4">Guide</Table.Head>
                                        <Table.Head class="text-right">Started</Table.Head>
                                        <Table.Head class="text-right">Completed</Table.Head>
                                        <Table.Head class="text-right">Dismissed</Table.Head>
                                        <Table.Head>Entry points</Table.Head>
                                        <Table.Head>Last event</Table.Head>
                                    </Table.Row>
                                </Table.Header>
                                <Table.Body>
                                    {#each guides as tour (`${tour.name}:${tour.version}`)}
                                        <Table.Row>
                                            <Table.Cell class="pl-4 font-medium"
                                                >{title(tour.name)} <span class="text-muted-foreground font-normal">v{tour.version}</span></Table.Cell
                                            >
                                            <Table.Cell class="text-right"><Number value={tour.started} /></Table.Cell>
                                            {@render RateCell(tour.completed, getOutcomeShare(tour, 'completed'))}
                                            {@render RateCell(tour.dismissed, getOutcomeShare(tour, 'dismissed'))}
                                            <Table.Cell>{@render SourceMix(tour)}</Table.Cell>
                                            {@render LastRun(tour.last_run_utc)}
                                        </Table.Row>
                                    {/each}
                                </Table.Body>
                            </Table.Root>
                        </div>
                        <div class="grid gap-3 px-4 md:hidden">
                            {#each guides as tour (`${tour.name}:${tour.version}`)}
                                {@render GuideCard(tour)}
                            {/each}
                        </div>
                    </Card.Content>
                </Card.Root>
            {/if}
        {/if}
    {/if}
</div>

{#snippet RateCell(value: number, rate: null | number)}
    <Table.Cell class="text-right">
        <Number {value} />
        <span class="text-muted-foreground ml-1 text-xs"
            >({#if rate === null}—{:else}<Percentage percent={rate * 100} />{/if})</span
        >
    </Table.Cell>
{/snippet}

{#snippet LastRun(value: null | string | undefined)}
    <Table.Cell>
        {#if value}
            <TimeAgo {value} />
        {:else}
            <span class="text-muted-foreground">Never</span>
        {/if}
    </Table.Cell>
{/snippet}

{#snippet SourceMix(tour: ProductTourSummary)}
    {#if tour.started === 0 || tour.start_sources.length === 0}
        <span class="text-muted-foreground">—</span>
    {:else}
        <div class="flex flex-wrap gap-1">
            {#each tour.start_sources as source (source.source)}
                <Badge variant="outline">
                    {formatSource(source.source)}
                    <Number value={source.count} />
                    (<Percentage percent={(getStartSourceShare(source, tour.started) ?? 0) * 100} />)
                </Badge>
            {/each}
        </div>
    {/if}
{/snippet}

{#snippet PromptCard(tour: ProductTourSummary)}
    <div class="rounded-lg border p-4">
        <div class="flex items-start justify-between gap-3">
            <div class="font-medium">{title(tour.name)}</div>
            <Badge variant="outline">v{tour.version}</Badge>
        </div>
        <div class="mt-4 grid grid-cols-2 gap-3 text-sm">
            {@render Metric('Shown', tour.shown)}
            {@render Metric('Started', tour.started, promptStart(tour))}
            {@render Metric('Engaged', tour.completed, promptEngagement(tour))}
            {@render Metric('Dismissed', tour.dismissed, promptDismissal(tour))}
        </div>
        {#if tour.last_run_utc}
            <p class="text-muted-foreground mt-4 text-xs">Last event <TimeAgo value={tour.last_run_utc} /></p>
        {/if}
    </div>
{/snippet}

{#snippet GuideCard(tour: ProductTourSummary)}
    <div class="rounded-lg border p-4">
        <div class="flex items-start justify-between gap-3">
            <div class="font-medium">{title(tour.name)}</div>
            <Badge variant="outline">v{tour.version}</Badge>
        </div>
        <div class="mt-4 grid grid-cols-2 gap-3 text-sm">
            {@render Metric('Started', tour.started)}
            {@render Metric('Completed', tour.completed, getOutcomeShare(tour, 'completed'))}
            {@render Metric('Dismissed', tour.dismissed, getOutcomeShare(tour, 'dismissed'))}
        </div>
        <div class="mt-4">
            <p class="text-muted-foreground mb-2 text-xs font-medium">Entry points</p>
            {@render SourceMix(tour)}
        </div>
        {#if tour.last_run_utc}
            <p class="text-muted-foreground mt-4 text-xs">Last event <TimeAgo value={tour.last_run_utc} /></p>
        {/if}
    </div>
{/snippet}

{#snippet Metric(label: string, value: number, rate?: null | number)}
    <div>
        <div class="text-muted-foreground text-xs">{label}</div>
        <div class="font-medium">
            <Number {value} />
            {#if rate !== undefined}<span class="text-muted-foreground text-xs"
                    >({#if rate === null}—{:else}<Percentage percent={rate * 100} />{/if})</span
                >{/if}
        </div>
    </div>
{/snippet}
