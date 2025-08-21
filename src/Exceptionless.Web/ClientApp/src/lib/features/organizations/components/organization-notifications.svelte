<script lang="ts">
    import { goto } from '$app/navigation';
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { organization as currentOrganizationId } from '$features/organizations/context.svelte';
    import { SuspensionCode } from '$features/organizations/models';
    import { getProjectsQuery } from '$features/projects/api.svelte';

    import FreePlanNotification from './notifications/free-plan-notification.svelte';
    import HourlyOverageNotification from './notifications/hourly-overage-notification.svelte';
    import LoadingOrganizationNotification from './notifications/loading-organization-notification.svelte';
    import MonthlyOverageNotification from './notifications/monthly-overage-notification.svelte';
    import NoProjectsNotification from './notifications/no-projects-notification.svelte';
    import PremiumUpgradeNotification from './notifications/premium-upgrade-notification.svelte';
    import ProjectConfigurationNotification from './notifications/project-configuration-notification.svelte';
    import RequestLimitNotification from './notifications/request-limit-notification.svelte';
    import SuspendedOrganizationNotification from './notifications/suspended-organization-notification.svelte';

    interface Props {
        ignoreConfigureProjects?: boolean;
        ignoreFree?: boolean;
        requiresPremium?: boolean;
    }

    let { ignoreConfigureProjects = false, ignoreFree = false, requiresPremium = false }: Props = $props();

    const organizationsQuery = getOrganizationsQuery({});
    const projectsQuery = getProjectsQuery({});

    const org = $derived((organizationsQuery.data?.data ?? []).find((o) => o.id === currentOrganizationId.current));
    const orgProjects = $derived((projectsQuery.data?.data ?? []).filter((p) => p.organization_id === org?.id));
    const projectsNeedingConfig = $derived((orgProjects.filter((p) => p.is_configured === false)));

    // Status booleans
    const suspensionCode: SuspensionCode | undefined = $derived(
        org?.suspension_code === 'Billing'
            ? SuspensionCode.Billing
            : org?.suspension_code === 'Overage'
              ? SuspensionCode.Overage
              : org?.suspension_code === 'Abuse'
                ? SuspensionCode.Abuse
                : org?.suspension_code === 'Other'
                  ? SuspensionCode.Other
                  : undefined
    );

    const isSuspended = $derived(!!org?.is_suspended);
    const isSuspendedForBilling = $derived(isSuspended && suspensionCode === SuspensionCode.Billing);
    const isMonthlyOverage = $derived(!!org?.is_over_monthly_limit);
    const isHourlyOverage = $derived(!!org?.is_throttled);
    const isExceededRequestLimit = $derived(!!org?.is_over_request_limit);
    const isFreePlan = $derived(!ignoreFree && /free/i.test(org?.plan_name ?? ''));
    const hasNoProjects = $derived((orgProjects?.length ?? 0) === 0);
    const needsProjectConfiguration = $derived(!ignoreConfigureProjects && orgProjects?.length > 0 && projectsNeedingConfig.length === orgProjects.length);
    const requiresPremiumUpgrade = $derived(requiresPremium && !org?.has_premium_features && !needsProjectConfiguration);

    function goToBilling() {
        if (org?.id) {
            goto(`/next/organization/${org.id}/billing`);
        }
    }
</script>

{#if organizationsQuery.isLoading || projectsQuery.isLoading}
    <div class="space-y-2">
        <LoadingOrganizationNotification />
    </div>
{:else if org}
    {#if isSuspended}
        <SuspendedOrganizationNotification name={org.name} {suspensionCode} isBilling={isSuspendedForBilling} manageBilling={goToBilling} />
    {:else if isMonthlyOverage}
        <MonthlyOverageNotification name={org.name} changePlan={goToBilling} />
    {:else if isHourlyOverage}
        <HourlyOverageNotification name={org.name} viewPlan={goToBilling} />
    {:else if isExceededRequestLimit}
        <RequestLimitNotification name={org.name} />
    {:else if needsProjectConfiguration}
        <ProjectConfigurationNotification projects={projectsNeedingConfig} />
    {:else if requiresPremiumUpgrade}
        <PremiumUpgradeNotification name={org.name} upgradePlan={goToBilling} />
    {:else if isFreePlan}
        <FreePlanNotification name={org.name} changePlan={goToBilling} />
    {:else if hasNoProjects}
        <NoProjectsNotification name={org.name} />
    {/if}
{/if}
