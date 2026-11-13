<script lang="ts">
    import { PUBLIC_STRIPE_PUBLISHABLE_KEY } from '$app/env/public';
    import { page } from '$app/state';

    import ErrorMessage from '#comp/error-message.svelte';
    import Number from '#comp/formatters/number.svelte';
    import TimeAgo from '#comp/formatters/time-ago.svelte';
    import { Muted, Small } from '#comp/typography/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import { Skeleton } from '#comp/ui/skeleton/index.ts';
    import { ChangePlanDialog } from '#features/billing/index.ts';
    import { getOrganizationQuery } from '#features/organizations/api.svelte.ts';
    import UsageChart from '#features/organizations/components/usage-chart.svelte';
    import { getNextBillingDateUtc, getRemainingEventLimit, ORGANIZATION_USAGE_REFETCH_INTERVAL_MS } from '#features/organizations/utils.ts';
    import { formatLongDate } from '#shared/dates.ts';

    const organizationQuery = getOrganizationQuery({
        refetchInterval: ORGANIZATION_USAGE_REFETCH_INTERVAL_MS,
        route: {
            get id() {
                return page.params.organizationId || '';
            }
        }
    });

    const hasMonthlyUsage = $derived((organizationQuery.data?.max_events_per_month ?? 0) > 0);
    const canChangePlan = $derived(organizationQuery.isSuccess && !!PUBLIC_STRIPE_PUBLISHABLE_KEY);
    const remainingEventLimit = $derived(getRemainingEventLimit(organizationQuery.data));
    const nextBillingDate = $derived(getNextBillingDateUtc(organizationQuery.data));

    let changePlanDialogOpen = $state(false);

    const chartData = $derived.by(() => {
        const organization = organizationQuery.data;

        if (!organization?.usage) {
            return [];
        }

        return organization.usage.map((usage) => {
            return {
                ...usage,
                date: new Date(usage.date)
            };
        });
    });
</script>

<div class="space-y-6">
    <Muted>View your historical usage</Muted>

    {#if organizationQuery.isLoading}
        <div class="space-y-4">
            <Skeleton class="h-12 w-3/4" />
            <Skeleton class="h-50 w-full" />
            <Skeleton class="h-6 w-1/3" />
        </div>
    {:else if organizationQuery.error}
        <ErrorMessage message="Unable to load usage data." />
    {:else if !hasMonthlyUsage}
        <Muted>Monthly usage is not available for this organization. Please contact support for more information</Muted>
    {:else}
        <div class="space-y-6">
            <div class="bg-muted/20 rounded-md border p-4">
                <p>
                    You are currently on the
                    {#if canChangePlan}
                        <Button variant="link" class="h-auto p-0" onclick={() => (changePlanDialogOpen = true)}>
                            <span class="font-bold">{organizationQuery.data?.plan_name}</span> plan</Button
                        >
                    {:else}
                        <span class="font-bold">{organizationQuery.data?.plan_name}</span> plan
                    {/if}
                    with
                    <span class={remainingEventLimit === 0 ? 'text-destructive font-bold' : 'font-bold'}>
                        <Number value={remainingEventLimit} />
                    </span>
                    events remaining until this billing period's limit is reset on
                    <span class="font-bold">{formatLongDate(nextBillingDate)}</span>
                    (<TimeAgo value={nextBillingDate} />).

                    {#if canChangePlan}
                        <Button variant="link" class="h-auto p-0" onclick={() => (changePlanDialogOpen = true)}
                            >Click here to change your plan or billing information.</Button
                        >
                    {/if}
                </p>
            </div>

            <UsageChart data={chartData} />

            <Small class="text-xs">The usage data above is refreshed periodically and may not reflect current totals.</Small>
        </div>
    {/if}
</div>

{#if changePlanDialogOpen && organizationQuery.data}
    <ChangePlanDialog organization={organizationQuery.data} onclose={() => (changePlanDialogOpen = false)} />
{/if}
