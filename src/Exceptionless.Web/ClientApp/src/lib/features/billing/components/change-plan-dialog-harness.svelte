<script lang="ts">
    import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
    import { untrack } from 'svelte';

    import type { ViewOrganization } from '#features/organizations/models.ts';
    import type { BillingPlan } from '#lib/generated/api.ts';

    import { Button } from '#comp/ui/button/index.ts';
    import { accessToken } from '#features/auth/index.svelte.ts';
    import { queryKeys } from '#features/organizations/api.svelte.ts';

    import ChangePlanDialog from './change-plan-dialog.svelte';

    interface Props {
        initialCouponCode?: string;
        initialCouponOpen?: boolean;
        initialFormError?: string;
        initialPlanId?: string;
        organization: ViewOrganization;
        plans: BillingPlan[];
    }

    let { initialCouponCode, initialCouponOpen, initialFormError, initialPlanId, organization, plans }: Props = $props();

    accessToken.current = 'storybook-mock-token';

    const hasPlans = untrack(() => plans.length > 0);

    const queryClient = new QueryClient({
        defaultOptions: {
            queries: {
                enabled: hasPlans,
                refetchOnMount: false,
                refetchOnWindowFocus: false,
                retry: false,
                staleTime: Infinity
            }
        }
    });

    untrack(() => {
        if (plans.length > 0) {
            queryClient.setQueryData(queryKeys.plans(organization.id), plans);
        }
    });

    let open = $state(true);

    function handleClose() {
        open = false;
    }
</script>

<QueryClientProvider client={queryClient}>
    <Button variant="outline" onclick={() => (open = true)}>Open dialog</Button>

    {#if open}
        <ChangePlanDialog onclose={handleClose} {organization} {initialCouponCode} {initialCouponOpen} {initialFormError} {initialPlanId} />
    {/if}
</QueryClientProvider>
