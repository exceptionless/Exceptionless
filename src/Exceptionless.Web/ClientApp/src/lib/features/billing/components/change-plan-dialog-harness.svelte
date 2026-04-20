<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { BillingPlan } from '$lib/generated/api';

    import { Button } from '$comp/ui/button';
    import { accessToken } from '$features/auth/index.svelte';
    import { queryKeys } from '$features/organizations/api.svelte';
    import { QueryClient, QueryClientProvider } from '@tanstack/svelte-query';
    import { untrack } from 'svelte';

    import ChangePlanDialog from './change-plan-dialog.svelte';

    interface Props {
        organization: ViewOrganization;
        plans: BillingPlan[];
    }

    let { organization, plans }: Props = $props();

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
</script>

<QueryClientProvider client={queryClient}>
    <Button variant="outline" onclick={() => (open = true)}>Open dialog</Button>

    <ChangePlanDialog bind:open {organization} />
</QueryClientProvider>
