<script lang="ts">
    import { resolve } from '$app/paths';
    import Currency from '$comp/formatters/currency.svelte';
    import NumberCompact from '$comp/formatters/number-compact.svelte';
    import Number from '$comp/formatters/number.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import * as Card from '$comp/ui/card';
    import { Input } from '$comp/ui/input';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { getAdminAssistantUsageQuery } from '$features/admin/api.svelte';
    import { getBlockedCount, getTotalTokens, getUsageRisk, getUtcMonthKey, type UsageRisk } from '$features/admin/assistant-usage';
    import Bot from '@lucide/svelte/icons/bot';
    import Building2 from '@lucide/svelte/icons/building-2';
    import Coins from '@lucide/svelte/icons/coins';
    import Gauge from '@lucide/svelte/icons/gauge';
    import MessagesSquare from '@lucide/svelte/icons/messages-square';
    import Wrench from '@lucide/svelte/icons/wrench';

    function riskBadgeVariant(risk: UsageRisk): 'destructive' | 'outline' | 'yellow' {
        if (risk === 'critical') {
            return 'destructive';
        }

        return risk === 'warning' ? 'yellow' : 'outline';
    }

    function utilizationLabel(utilization: null | number | undefined): string {
        return utilization === null || utilization === undefined ? 'No limit' : `${Math.round(utilization * 100)}%`;
    }

    const currentMonth = getUtcMonthKey();
    let selectedMonth = $state(currentMonth);
    const usageQuery = getAdminAssistantUsageQuery(() => selectedMonth);
    const usage = $derived(usageQuery.data);
    const totalTokens = $derived((usage?.prompt_tokens ?? 0) + (usage?.completion_tokens ?? 0));
    const averageCost = $derived(usage && usage.active_organizations > 0 ? usage.cost_usd / usage.active_organizations : 0);
    const tokensPerTurn = $derived(usage && usage.turns > 0 ? totalTokens / usage.turns : 0);

    const statCards = $derived([
        { icon: Building2, label: 'Active Organizations', value: usage?.active_organizations, valueType: 'number' },
        { icon: MessagesSquare, label: 'Turns', value: usage?.turns, valueType: 'number' },
        { icon: Bot, label: 'Tokens', value: totalTokens, valueType: 'compact' },
        { icon: Coins, label: 'Total Provider Cost', sub: 'across all organizations', value: usage?.cost_usd, valueType: 'currency' },
        { icon: Gauge, label: 'Tokens per Turn', value: tokensPerTurn, valueType: 'number' },
        { icon: Wrench, label: 'Average Cost', sub: 'per active organization', value: averageCost, valueType: 'currency' }
    ]);
</script>

<div class="space-y-6">
    <div class="flex flex-wrap items-end justify-between gap-4">
        <Muted>Monthly Exie usage, provider cost, and plan-limit health across all organizations</Muted>
        <label class="flex flex-col gap-1 text-sm font-medium">
            Month
            <Input class="w-40" type="month" max={currentMonth} bind:value={selectedMonth} />
        </label>
    </div>

    {#if usageQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load Exie usage. Please try again.</p>
            </Card.Content>
        </Card.Root>
    {:else}
        <div class="grid grid-cols-2 gap-4 sm:grid-cols-3">
            {#each statCards as card (card.label)}
                {@const Icon = card.icon}
                <Card.Root class="flex flex-col justify-between">
                    <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                        <Card.Title class="text-sm font-medium">{card.label}</Card.Title>
                        <Icon class="text-muted-foreground size-4" aria-hidden="true" />
                    </Card.Header>
                    <Card.Content>
                        {#if usageQuery.isPending}
                            <Skeleton class="h-8 w-24 rounded" />
                        {:else}
                            <div class="text-2xl font-bold">
                                {#if card.valueType === 'currency'}
                                    <Currency value={card.value ?? null} />
                                {:else if card.valueType === 'compact'}
                                    <NumberCompact value={card.value ?? null} />
                                {:else}
                                    <Number value={card.value ?? null} />
                                {/if}
                            </div>
                            {#if card.sub}
                                <p class="text-muted-foreground mt-0.5 text-xs">{card.sub}</p>
                            {/if}
                        {/if}
                    </Card.Content>
                </Card.Root>
            {/each}
        </div>

        <Card.Root>
            <Card.Header class="flex flex-row flex-wrap items-start justify-between gap-3">
                <div class="space-y-1">
                    <Card.Title>Organization Usage</Card.Title>
                    <Card.Description>Usage is ordered by provider cost, then token consumption. Durable totals may lag by up to five minutes.</Card.Description
                    >
                </div>
            </Card.Header>
            <Card.Content class="px-0">
                {#if usageQuery.isPending}
                    <div class="space-y-3 px-4 pb-2">
                        {#each [0, 1, 2, 3, 4] as row (row)}
                            <Skeleton class="h-12 w-full rounded" aria-label={`Loading organization ${row + 1}`} />
                        {/each}
                    </div>
                {:else if usage?.organizations.length === 0}
                    <p class="text-muted-foreground px-4 py-10 text-center text-sm">No Exie usage was recorded for this month.</p>
                {:else}
                    <Table.Root>
                        <Table.Header>
                            <Table.Row>
                                <Table.Head class="pl-4">Organization</Table.Head>
                                <Table.Head>Last Used</Table.Head>
                                <Table.Head class="text-right">Activity</Table.Head>
                                <Table.Head class="text-right">Tokens</Table.Head>
                                <Table.Head>Token Limit</Table.Head>
                                <Table.Head class="text-right">Cost</Table.Head>
                                <Table.Head>Cost Limit</Table.Head>
                                <Table.Head>Outcomes</Table.Head>
                                <Table.Head class="pr-4 text-right">Blocked</Table.Head>
                            </Table.Row>
                        </Table.Header>
                        <Table.Body>
                            {#each usage?.organizations ?? [] as organization (organization.organization_id)}
                                {@const blockedCount = getBlockedCount(organization)}
                                {@const tokenRisk = getUsageRisk(organization.token_utilization, organization.blocked_by_token_limit)}
                                {@const costRisk = getUsageRisk(organization.cost_utilization, organization.blocked_by_cost_limit)}
                                <Table.Row>
                                    <Table.Cell class="max-w-64 pl-4 whitespace-normal">
                                        <a
                                            class="text-primary font-medium underline-offset-4 hover:underline"
                                            href={resolve('/(app)/organization/[organizationId]/manage', { organizationId: organization.organization_id })}
                                        >
                                            {organization.organization_name}
                                        </a>
                                        <div class="text-muted-foreground mt-0.5 text-xs">{organization.plan_id}</div>
                                    </Table.Cell>
                                    <Table.Cell><TimeAgo value={organization.last_used_utc} /></Table.Cell>
                                    <Table.Cell class="text-right">
                                        <div><Number value={organization.turns} /> turns</div>
                                        <div class="text-muted-foreground mt-0.5 text-xs">
                                            <Number value={organization.provider_requests} /> requests · <Number value={organization.tool_calls} /> tools
                                        </div>
                                    </Table.Cell>
                                    <Table.Cell
                                        class="text-right"
                                        title={`${organization.prompt_tokens.toLocaleString()} prompt, ${organization.completion_tokens.toLocaleString()} completion`}
                                    >
                                        <NumberCompact value={getTotalTokens(organization)} />
                                    </Table.Cell>
                                    <Table.Cell>
                                        <Badge variant={riskBadgeVariant(tokenRisk)}>{utilizationLabel(organization.token_utilization)}</Badge>
                                        {#if organization.monthly_token_limit}
                                            <span class="text-muted-foreground ml-1 text-xs">of <NumberCompact value={organization.monthly_token_limit} /></span
                                            >
                                        {/if}
                                    </Table.Cell>
                                    <Table.Cell class="text-right"><Currency value={organization.cost_usd} /></Table.Cell>
                                    <Table.Cell>
                                        <Badge variant={riskBadgeVariant(costRisk)}>{utilizationLabel(organization.cost_utilization)}</Badge>
                                        {#if organization.monthly_cost_limit_usd}
                                            <span class="text-muted-foreground ml-1 text-xs">of <Currency value={organization.monthly_cost_limit_usd} /></span>
                                        {/if}
                                    </Table.Cell>
                                    <Table.Cell>
                                        <span class="text-emerald-600 dark:text-emerald-400"><Number value={organization.completed} /></span>
                                        <span class="text-muted-foreground"> / </span>
                                        <span class={organization.failed > 0 ? 'text-destructive' : 'text-muted-foreground'}
                                            ><Number value={organization.failed} /></span
                                        >
                                        <span class="text-muted-foreground"> / <Number value={organization.cancelled} /></span>
                                        <span class="sr-only"> completed / failed / cancelled</span>
                                    </Table.Cell>
                                    <Table.Cell class="pr-4 text-right">
                                        {#if blockedCount > 0}
                                            <Badge variant="destructive">{blockedCount}</Badge>
                                        {:else}
                                            <span class="text-muted-foreground">—</span>
                                        {/if}
                                    </Table.Cell>
                                </Table.Row>
                            {/each}
                        </Table.Body>
                    </Table.Root>
                {/if}
            </Card.Content>
        </Card.Root>
    {/if}
</div>
