<script lang="ts">
    import type { NotificationProps } from '$comp/notification';

    import { getOrganizationQuery, getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { organization as currentOrganizationId } from '$features/organizations/context.svelte';
    import { SuspensionCode } from '$features/organizations/models';
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';
    import { getMeQuery } from '$features/users/api.svelte';

    import FreePlanNotification from './notifications/free-plan-notification.svelte';
    import HourlyOverageNotification from './notifications/hourly-overage-notification.svelte';
    import ImpersonationNotification from './notifications/impersonation-notification.svelte';
    import MonthlyOverageNotification from './notifications/monthly-overage-notification.svelte';
    import PremiumUpgradeNotification from './notifications/premium-upgrade-notification.svelte';
    import ProjectConfigurationNotification from './notifications/project-configuration-notification.svelte';
    import RequestLimitNotification from './notifications/request-limit-notification.svelte';
    import SetupFirstProjectNotification from './notifications/setup-first-project-notification.svelte';
    import SuspendedOrganizationNotification from './notifications/suspended-organization-notification.svelte';

    interface Props extends NotificationProps {
        ignoreConfigureProjects?: boolean;
        ignoreFree?: boolean;
        isChatEnabled: boolean;
        openChat: () => void;
        requiresPremium?: boolean;
    }

    let { ignoreConfigureProjects = false, ignoreFree = false, isChatEnabled, openChat, requiresPremium = false, ...restProps }: Props = $props();

    // Store the organizationId to prevent loading when switching organizations.
    const organizationId = currentOrganizationId.current;

    const meQuery = getMeQuery();
    const isGlobalAdmin = $derived(!!meQuery.data?.roles?.includes('global'));

    const userOrganizationIds = $derived(meQuery.data?.organization_ids ?? []);
    const isImpersonating = $derived(isGlobalAdmin && organizationId !== undefined && !userOrganizationIds.includes(organizationId));

    const organizationsQuery = getOrganizationsQuery({});
    const userOrganizations = $derived((organizationsQuery.data?.data ?? []).filter((org) => userOrganizationIds.includes(org.id!)));

    const organizationQuery = getOrganizationQuery({
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

    const organization = $derived(organizationQuery.data);
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
    const hasNoProjects = $derived(projectsQuery.isSuccess && projects.length === 0);
    const needsProjectConfiguration = $derived(
        projectsQuery.isSuccess && !ignoreConfigureProjects && projects.length > 0 && projectsNeedingConfig.length === projects.length
    );
    const requiresPremiumUpgrade = $derived(requiresPremium && !organization?.has_premium_features && !needsProjectConfiguration);
</script>

{#if isImpersonating && organization}
    <ImpersonationNotification name={organization.name} {userOrganizations} {...restProps} />
{/if}

{#if organization}
    {#if isSuspended}
        <SuspendedOrganizationNotification
            name={organization.name}
            {suspensionCode}
            {isChatEnabled}
            {openChat}
            organizationId={organization.id}
            {...restProps}
        />
    {:else if isMonthlyOverage}
        <MonthlyOverageNotification name={organization.name} organizationId={organization.id} {...restProps} />
    {:else if isHourlyOverage}
        <HourlyOverageNotification name={organization.name} organizationId={organization.id} {...restProps} />
    {:else if isExceededRequestLimit}
        <RequestLimitNotification name={organization.name} {isChatEnabled} {openChat} {...restProps} />
    {:else if needsProjectConfiguration}
        <ProjectConfigurationNotification projects={projectsNeedingConfig} {...restProps} />
    {:else if requiresPremiumUpgrade}
        <PremiumUpgradeNotification name={organization.name} organizationId={organization.id} {...restProps} />
    {:else if isFreePlan}
        <FreePlanNotification name={organization.name} organizationId={organization.id} {...restProps} />
    {:else if hasNoProjects}
        <SetupFirstProjectNotification {...restProps} />
    {/if}
{/if}
