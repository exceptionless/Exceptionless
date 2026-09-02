<script lang="ts">
    import type { ProductTourSummary } from '$generated/api';

    import Number from '$comp/formatters/number.svelte';
    import Percentage from '$comp/formatters/percentage.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Select from '$comp/ui/select';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { getAdminProductTourUsageQuery } from '$features/admin/api.svelte';
    import { getUtcMonthKey } from '$features/admin/assistant-usage';
    import ProductTourActivity from '$features/admin/components/product-tour-activity.svelte';
    import ProductTourPeriod from '$features/admin/components/product-tour-period.svelte';
    import { getGuideOutcomeRate, getRate, getStartSourceShare, type ProductTourUsageRange } from '$features/admin/product-tour-usage';
    import { productTourCatalog } from '$features/product-tours/catalog';

    const currentMonth = getUtcMonthKey();
    let range = $state<ProductTourUsageRange>({
        kind: 'month',
        month: currentMonth
    });
    const usageQuery = getAdminProductTourUsageQuery(() => range);
    const usage = $derived(usageQuery.data);
    let selectedTour = $state('app-overview:1');
    const activeTour = $derived(usage?.tours.find((tour) => `${tour.name}:${tour.version}` === selectedTour));

    function title(value: string): string {
        const guide = productTourCatalog.find((tour) => tour.name === value);
        if (guide) {
            return guide.title;
        }

        if (value === 'app-welcome') {
            return 'Welcome invitation';
        }

        if (value === 'exie-announcement') {
            return 'Exie invitation';
        }
        return value
            .split('-')
            .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
            .join(' ');
    }

    function formatSource(source: string): string {
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
                return title(source);
        }
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

<div class="flex flex-col gap-4">
    <Muted>See which guides people start, finish, or dismiss.</Muted>
    <div class="flex items-center gap-2" aria-label="Usage filters">
        <Select.Root type="single" bind:value={selectedTour}>
            <Select.Trigger aria-label="Guide or invitation" class="min-w-0 flex-1 sm:max-w-60">
                <span class="truncate">{activeTour ? `${title(activeTour.name)} · v${activeTour.version}` : 'Choose a guide'}</span>
            </Select.Trigger>
            <Select.Content>
                {#each ['guide', 'prompt'] as kind (kind)}
                    <Select.Group>
                        <Select.Label>{kind === 'guide' ? 'Guides' : 'Invitations'}</Select.Label>
                        {#each usage?.tours.filter((tour) => tour.kind === kind) ?? [] as tour (`${tour.name}:${tour.version}`)}
                            <Select.Item value={`${tour.name}:${tour.version}`}>{title(tour.name)} · v{tour.version}</Select.Item>
                        {/each}
                    </Select.Group>
                {/each}
            </Select.Content>
        </Select.Root>
        <ProductTourPeriod bind:range />
    </div>

    {#if usageQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load guided-tour usage. Please try again.</p>
                <Button class="mt-3" variant="outline" onclick={() => usageQuery.refetch()}>Retry</Button>
            </Card.Content>
        </Card.Root>
    {:else}
        {#if usageQuery.isPending}
            <Card.Root>
                <Card.Header>
                    <Card.Title>Guided-tour usage</Card.Title>
                </Card.Header>
                <Card.Content class="flex flex-col gap-3">
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

            {#if activeTour && usage}
                <Card.Root>
                    <Card.Content class="pt-4">
                        <ProductTourActivity tour={activeTour} interval={usage.interval} start={usage.utc_start} end={usage.utc_end} />
                    </Card.Content>
                    {#if activeTour.start_sources.length > 0 || activeTour.last_run_utc}
                        <Card.Footer class="flex flex-wrap items-center justify-between gap-2">
                            {#if activeTour.start_sources.length > 0}<div class="flex flex-col gap-1 text-xs">
                                    <p class="text-muted-foreground">How people opened this guide</p>
                                    {@render SourceMix(activeTour)}
                                </div>{/if}
                            {#if activeTour.last_run_utc}<p class="text-muted-foreground text-xs">
                                    Last activity <TimeAgo value={activeTour.last_run_utc} />
                                </p>{/if}
                        </Card.Footer>
                    {/if}
                </Card.Root>
                <p class="text-muted-foreground text-xs">
                    Counts are events, not unique people. Rates compare events in this period{activeTour.kind === 'prompt'
                        ? '; accepted includes starting a guide or browsing guides'
                        : ''}.
                </p>
            {/if}

            <details class="group">
                <summary class="cursor-pointer text-sm font-medium">Compare all guides and invitations</summary>
                <div class="mt-4 flex flex-col gap-4">
                    {#if prompts.length > 0}
                        <Card.Root>
                            <Card.Header>
                                <Card.Title>Guide invitations</Card.Title>
                                <Card.Description
                                    >The welcome and feature invitations that offer a guide. Percentages are based on how often each invitation was shown.</Card.Description
                                >
                            </Card.Header>
                            <Card.Content class="px-0">
                                <div class="hidden overflow-x-auto md:block">
                                    <Table.Root aria-label="Guide invitation usage" class="min-w-3xl">
                                        <Table.Header>
                                            <Table.Row>
                                                <Table.Head class="pl-4">Invitation</Table.Head>
                                                <Table.Head class="text-right">Shown</Table.Head>
                                                <Table.Head class="text-right">Started</Table.Head>
                                                <Table.Head class="text-right">Accepted</Table.Head>
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
                                <Card.Description>Completions and dismissals are shown as a percentage of starts in this period.</Card.Description>
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
                                                <Table.Head>Opened from</Table.Head>
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
                                                    {@render RateCell(tour.completed, getGuideOutcomeRate(tour, 'completed'))}
                                                    {@render RateCell(tour.dismissed, getGuideOutcomeRate(tour, 'dismissed'))}
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
                </div>
            </details>
        {/if}
    {/if}
</div>

{#snippet RateCell(value: number, rate: null | number)}
    <Table.Cell class="text-right">
        <Number {value} />
        {#if rate !== null}<span class="text-muted-foreground ml-1 text-xs">(<Percentage percent={rate * 100} />)</span>{/if}
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
        <span class="text-muted-foreground">No starts recorded</span>
    {:else}
        <div class="flex flex-wrap gap-x-4 gap-y-1">
            {#each tour.start_sources as source (source.source)}
                <span>
                    {formatSource(source.source)}
                    <Number value={source.count} />
                    (<Percentage percent={(getStartSourceShare(source, tour.started) ?? 0) * 100} />)
                </span>
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
            {@render Metric('Accepted', tour.completed, promptEngagement(tour))}
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
            {@render Metric('Completed', tour.completed, getGuideOutcomeRate(tour, 'completed'))}
            {@render Metric('Dismissed', tour.dismissed, getGuideOutcomeRate(tour, 'dismissed'))}
        </div>
        <div class="mt-4">
            <p class="text-muted-foreground mb-2 text-xs font-medium">Opened from</p>
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
            {#if rate !== undefined && rate !== null}<span class="text-muted-foreground text-xs">(<Percentage percent={rate * 100} />)</span>{/if}
        </div>
    </div>
{/snippet}
