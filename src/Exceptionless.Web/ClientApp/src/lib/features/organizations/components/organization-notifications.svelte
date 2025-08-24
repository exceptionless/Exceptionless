<script lang="ts">
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { organization as currentOrganizationId } from '$features/organizations/context.svelte';
    import { SuspensionCode } from '$features/organizations/models';
    import { type GetOrganizationProjectsParams, getOrganizationProjectsQuery } from '$features/projects/api.svelte';

    import FreePlanNotification from './notifications/free-plan-notification.svelte';
    import HourlyOverageNotification from './notifications/hourly-overage-notification.svelte';
    import MonthlyOverageNotification from './notifications/monthly-overage-notification.svelte';
    import PremiumUpgradeNotification from './notifications/premium-upgrade-notification.svelte';
    import ProjectConfigurationNotification from './notifications/project-configuration-notification.svelte';
    import RequestLimitNotification from './notifications/request-limit-notification.svelte';
    import SetupFirstProjectNotification from './notifications/setup-first-project-notification.svelte';
    import SuspendedOrganizationNotification from './notifications/suspended-organization-notification.svelte';

    interface Props {
        ignoreConfigureProjects?: boolean;
        ignoreFree?: boolean;
        isChatEnabled: boolean;
        openChat: () => void;
        requiresPremium?: boolean;
    }

    let { ignoreConfigureProjects = false, ignoreFree = false, isChatEnabled, openChat, requiresPremium = false }: Props = $props();

    // Store the organizationId to prevent loading when switching organizations.
    const organizationId = currentOrganizationId.current;

    const organizationsQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const projectsQuery = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const organization = $derived(organizationsQuery.data);
    const projects = $derived((projectsQuery.data?.data ?? []).filter((p) => p.organization_id === organizationId));
    const projectsNeedingConfig = $derived(projects.filter((p) => p.is_configured === false));

    const suspensionCode: SuspensionCode | undefined = $derived(
        organization?.suspension_code === 'Billing'
            ? SuspensionCode.Billing
            : organization?.suspension_code === 'Overage'
              ? SuspensionCode.Overage
              : organization?.suspension_code === 'Abuse'
                ? SuspensionCode.Abuse
                : organization?.suspension_code === 'Other'
                  ? SuspensionCode.Other
                  : undefined
    );

    const isSuspended = $derived(!!organization?.is_suspended);
    const isMonthlyOverage = $derived(!!organization?.is_over_monthly_limit);
    const isHourlyOverage = $derived(!!organization?.is_throttled);
    const isExceededRequestLimit = $derived(!!organization?.is_over_request_limit);
    const isFreePlan = $derived(!ignoreFree && /free/i.test(organization?.plan_name ?? ''));
    const hasNoProjects = $derived((projects?.length ?? 0) === 0);
    const needsProjectConfiguration = $derived(!ignoreConfigureProjects && projects?.length > 0 && projectsNeedingConfig.length === projects.length);
    const requiresPremiumUpgrade = $derived(requiresPremium && !organization?.has_premium_features && !needsProjectConfiguration);
</script>

{#if organization}
    {#if isSuspended}
        <SuspendedOrganizationNotification name={organization.name} {suspensionCode} {isChatEnabled} {openChat} organizationId={organization.id} />
    {:else if isMonthlyOverage}
        <MonthlyOverageNotification name={organization.name} organizationId={organization.id} />
    {:else if isHourlyOverage}
        <HourlyOverageNotification name={organization.name} organizationId={organization.id} />
    {:else if isExceededRequestLimit}
        <RequestLimitNotification name={organization.name} {isChatEnabled} {openChat} />
    {:else if needsProjectConfiguration}
        <ProjectConfigurationNotification projects={projectsNeedingConfig} />
    {:else if requiresPremiumUpgrade}
        <PremiumUpgradeNotification name={organization.name} organizationId={organization.id} />
    {:else if isFreePlan}
        <FreePlanNotification name={organization.name} organizationId={organization.id} />
    {:else if hasNoProjects}
        <SetupFirstProjectNotification />
    {/if}
{/if}
