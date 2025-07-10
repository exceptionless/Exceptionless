<script lang="ts">
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { A, H4, Muted, Small } from '$comp/typography';
    import * as Chart from '$comp/ui/chart/index';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import { env } from '$env/dynamic/public';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { getNextBillingDateUtc, getRemainingEventLimit } from '$features/organizations/utils';
    import { formatDateLabel, formatLongDate } from '$shared/dates';
    import { scaleUtc } from 'd3-scale';
    import { curveNatural } from 'd3-shape';
    import { AreaChart } from 'layerchart';

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return page.params.organizationId || '';
            }
        }
    });

    const hasMonthlyUsage = $derived((organizationQuery.data?.max_events_per_month ?? 0) > 0);
    const canChangePlan = $derived(organizationQuery.isSuccess && !!env.PUBLIC_STRIPE_PUBLISHABLE_KEY);
    const remainingEventLimit = $derived(getRemainingEventLimit(organizationQuery.data));
    const nextBillingDate = $derived(getNextBillingDateUtc(organizationQuery.data));

    function handleChangePlan() {
        // Navigate to plan change page or open modal
        // This is a placeholder for future implementation
        console.log('Change plan clicked');
    }

    const chartConfig = {
        blocked: { color: 'var(--chart-2)', label: 'Blocked' },
        discarded: { color: 'var(--chart-3)', label: 'Discarded' },
        limit: { color: 'var(--chart-6)', label: 'Limit' },
        too_big: { color: 'var(--chart-4)', label: 'Too Big' },
        total: { color: 'var(--chart-1)', label: 'Total' }
    } satisfies Chart.ChartConfig;

    const chartData = $derived.by(() => {
        const organization = organizationQuery.data;
        const org = organizationQuery.data;

        if (!organization?.usage || !org?.usage) {
            return [];
        }

        return organization.usage.map((o) => {
            return {
                ...o,
                date: new Date(o.date)
            };
        });
    });

    const series = [
        { key: 'total', ...chartConfig.total },
        { key: 'discarded', ...chartConfig.discarded },
        { key: 'blocked', ...chartConfig.blocked },
        { key: 'too_big', ...chartConfig.too_big },
        {
            key: 'limit',
            ...chartConfig.limit,
            props: {
                class: 'fill-none',
                line: { class: '[stroke-dasharray:4]' }
            }
        }
    ];
</script>

<div class="space-y-6">
    <div>
        <H4>Monthly Usage</H4>
        <Muted>View your historical usage.</Muted>
    </div>
    <Separator />

    {#if organizationQuery.isLoading || organizationQuery.isLoading}
        <div class="space-y-4">
            <Skeleton class="h-12 w-3/4" />
            <Skeleton class="h-[200px] w-full" />
            <Skeleton class="h-6 w-1/3" />
        </div>
    {:else if organizationQuery.error || organizationQuery.error}
        <ErrorMessage message="Unable to load usage data." />
    {:else if !hasMonthlyUsage}
        <Muted>Monthly usage is not available for this organization. Please contact support for more information.</Muted>
    {:else}
        <div class="space-y-6">
            <div class="bg-muted/20 rounded-md border p-4">
                <p>
                    You are currently on the
                    {#if canChangePlan}
                        <A onclick={handleChangePlan}>
                            <span class="font-bold">{organizationQuery.data?.plan_name}</span> plan
                        </A>
                    {:else}
                        <span class="font-bold">{organizationQuery.data?.plan_name}</span> plan
                    {/if}
                    with
                    <span class={remainingEventLimit === 0 ? 'text-destructive font-bold' : 'font-bold'}>
                        {remainingEventLimit.toLocaleString()}
                    </span>
                    events remaining until this billing period's limit is reset on
                    <span class="font-bold">{formatLongDate(nextBillingDate)}</span>
                    (<TimeAgo value={nextBillingDate} />).

                    {#if canChangePlan}
                        <A onclick={handleChangePlan}>Click here to change your plan or billing information.</A>
                    {/if}
                </p>
            </div>

            <Chart.Container config={chartConfig} class="aspect-auto h-[250px] w-full">
                <AreaChart
                    legend
                    data={chartData}
                    x="date"
                    xScale={scaleUtc()}
                    {series}
                    props={{
                        area: {
                            curve: curveNatural
                        },
                        yAxis: { format: 'metric' }
                    }}
                >
                    {#snippet tooltip()}
                        <Chart.Tooltip class="min-w-[230px]" indicator="line" labelFormatter={(v: Date) => formatDateLabel(v)} />
                    {/snippet}
                </AreaChart>
            </Chart.Container>

            <Small class="text-xs">The usage data above is refreshed periodically and may not reflect current totals.</Small>
        </div>
    {/if}
</div>
