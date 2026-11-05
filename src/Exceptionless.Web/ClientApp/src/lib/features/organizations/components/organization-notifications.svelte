<script module lang="ts">
    import { ChangeType, type WebSocketMessageValue } from '#features/websockets/models.ts';

    export function recordConfiguredProjectId(
        message: WebSocketMessageValue<'PersistentEventChanged'>,
        organizationId: string | undefined,
        configuredProjectIds: Set<string>
    ): boolean {
        if (
            message.change_type === ChangeType.Removed ||
            message.organization_id !== organizationId ||
            !message.project_id ||
            configuredProjectIds.has(message.project_id)
        ) {
            return false;
        }

        configuredProjectIds.add(message.project_id);
        return true;
    }
</script>

<script lang="ts">
    import { useQueryClient } from '@tanstack/svelte-query';
    import { useEventListener, useInterval } from 'runed';
    import { SvelteSet } from 'svelte/reactivity';

    import type { NotificationProps } from '#comp/notification/index.ts';

    import { FREE_PLAN_ID, TRIAL_PLAN_ID } from '#features/billing/constants.ts';
    import { getTrialDaysRemaining, getTrialEndedNoticeKey, hasTrialEndedWithoutSubscribing, isTrialEndingSoon } from '#features/billing/trial.ts';
    import { getOrganizationQuery, getOrganizationsQuery } from '#features/organizations/api.svelte.ts';
    import { organization as currentOrganizationId } from '#features/organizations/context.svelte.ts';
    import { SuspensionCode } from '#features/organizations/models.ts';
    import { dismissOrganizationNotification, isOrganizationNotificationDismissed } from '#features/organizations/notification-dismissals.ts';
    import { getMonthlyEventUsage, isApproachingMonthlyEventLimit } from '#features/organizations/utils.ts';
    import { getProductTourRecordedAt } from '#features/product-tours/eligibility.ts';
    import { getOrganizationProjectsQuery } from '#features/projects/api.svelte.ts';
    import { getMeQuery, putCurrentUserProductTour, setCurrentUserProductTourRecorded, queryKeys as userQueryKeys } from '#features/users/api.svelte.ts';

    import HourlyOverageNotification from './notifications/hourly-overage-notification.svelte';
    import ImpersonationNotification from './notifications/impersonation-notification.svelte';
    import MonthlyOverageNotification from './notifications/monthly-overage-notification.svelte';
    import PremiumUpgradeNotification from './notifications/premium-upgrade-notification.svelte';
    import ProjectConfigurationNotification from './notifications/project-configuration-notification.svelte';
    import RequestLimitNotification from './notifications/request-limit-notification.svelte';
    import SetupFirstProjectNotification from './notifications/setup-first-project-notification.svelte';
    import SuspendedOrganizationNotification from './notifications/suspended-organization-notification.svelte';
    import TrialEndedNotification from './notifications/trial-ended-notification.svelte';
    import TrialNotification from './notifications/trial-notification.svelte';
    import UsageWarningNotification from './notifications/usage-warning-notification.svelte';

    interface Props extends NotificationProps {
        ignoreConfigureProjects?: boolean;
        isChatEnabled: boolean;
        openChat: () => void;
        premiumFeatureName?: string;
        requiresPremium?: boolean;
    }

    let { ignoreConfigureProjects = false, isChatEnabled, openChat, premiumFeatureName, requiresPremium = false, ...restProps }: Props = $props();

    const meQuery = getMeQuery();
    const isGlobalAdmin = $derived(!!meQuery.data?.roles?.includes('global'));

    const userOrganizationIds = $derived(meQuery.data?.organization_ids ?? []);
    const isImpersonating = $derived(
        isGlobalAdmin && currentOrganizationId.current !== undefined && !userOrganizationIds.includes(currentOrganizationId.current)
    );

    const organizationsQuery = getOrganizationsQuery({});
    const userOrganizations = $derived((organizationsQuery.data?.data ?? []).filter((org) => userOrganizationIds.includes(org.id!)));

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return currentOrganizationId.current;
            }
        }
    });

    const projectsQuery = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return currentOrganizationId.current;
            }
        }
    });

    const organization = $derived(organizationQuery.data);
    const projects = $derived((projectsQuery.data?.data ?? []).filter((p) => p.organization_id === currentOrganizationId.current));
    let configuredProjectIds = new SvelteSet<string>();
    const projectsNeedingConfig = $derived(projects.filter((p) => p.is_configured === false && !configuredProjectIds.has(p.id!)));

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
    const isFreePlan = $derived(organization?.plan_id === FREE_PLAN_ID);
    const isTrialPlan = $derived(organization?.plan_id === TRIAL_PLAN_ID);
    // Re-checks time-based notices each minute so day and month dismissals expire in a tab left open.
    let now = $state(new Date());
    useInterval(60 * 1000, {
        callback: () => (now = new Date())
    });

    const trialDaysRemaining = $derived(getTrialDaysRemaining(organization?.trial_ends_utc, now));
    const monthlyEventUsage = $derived(getMonthlyEventUsage(organization, now));

    const queryClient = useQueryClient();
    const recordProductTour = putCurrentUserProductTour();
    const isTrialEndedNoticeDismissed = $derived(
        !!organization?.id && !!getProductTourRecordedAt(meQuery.data?.product_tours, getTrialEndedNoticeKey(organization.id).replaceAll('-', '_'))
    );

    const showUsageWarning = $derived(
        isApproachingMonthlyEventLimit(organization, now) && !isOrganizationNotificationDismissed('usage-warning', organization?.id, now)
    );
    const showTrialEndingSoon = $derived(
        isTrialPlan && isTrialEndingSoon(trialDaysRemaining) && !isOrganizationNotificationDismissed('trial-ending', organization?.id, now)
    );
    // Admins impersonating an organization would otherwise fill their own product tour records with other organizations' notices.
    const showTrialEnded = $derived(
        isFreePlan && !isImpersonating && !!organization && hasTrialEndedWithoutSubscribing(organization) && !!meQuery.data && !isTrialEndedNoticeDismissed
    );
    const hasNoProjects = $derived(projectsQuery.isSuccess && projects.length === 0);
    const needsProjectConfiguration = $derived(
        projectsQuery.isSuccess && !ignoreConfigureProjects && projects.length > 0 && projectsNeedingConfig.length === projects.length
    );
    const requiresPremiumUpgrade = $derived(requiresPremium && !organization?.has_premium_features && !needsProjectConfiguration);

    function dismissTrialEnded(): void {
        const organizationId = organization?.id;
        const userId = meQuery.data?.id;
        if (!organizationId || !userId) {
            return;
        }

        const tourName = getTrialEndedNoticeKey(organizationId);
        setCurrentUserProductTourRecorded(queryClient, tourName, new Date().toISOString());
        recordProductTour.mutate(
            {
                tourName,
                userId
            },
            {
                // Put the notice back if the dismissal wasn't saved, rather than pretending it was.
                onError: () =>
                    queryClient.invalidateQueries({
                        queryKey: userQueryKeys.me()
                    })
            }
        );
    }

    useEventListener(document, 'PersistentEventChanged', (event) => {
        const message = (event as CustomEvent<WebSocketMessageValue<'PersistentEventChanged'>>).detail;
        recordConfiguredProjectId(message, currentOrganizationId.current, configuredProjectIds);
    });
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
        <PremiumUpgradeNotification name={organization.name} organizationId={organization.id} {premiumFeatureName} {...restProps} />
    {:else if showUsageWarning && monthlyEventUsage}
        <UsageWarningNotification
            limit={monthlyEventUsage.limit}
            name={organization.name}
            onDismiss={() => dismissOrganizationNotification('usage-warning', organization.id)}
            organizationId={organization.id}
            total={monthlyEventUsage.total}
            {...restProps}
        />
    {:else if showTrialEndingSoon}
        <TrialNotification
            daysRemaining={trialDaysRemaining}
            name={organization.name}
            onDismiss={() => dismissOrganizationNotification('trial-ending', organization.id)}
            organizationId={organization.id}
            {...restProps}
        />
    {:else if showTrialEnded}
        <TrialEndedNotification
            maxEventsPerMonth={organization.max_events_per_month}
            name={organization.name}
            onDismiss={dismissTrialEnded}
            organizationId={organization.id}
            retentionDays={organization.retention_days}
            {...restProps}
        />
    {:else if hasNoProjects}
        <SetupFirstProjectNotification {...restProps} />
    {/if}
{/if}
