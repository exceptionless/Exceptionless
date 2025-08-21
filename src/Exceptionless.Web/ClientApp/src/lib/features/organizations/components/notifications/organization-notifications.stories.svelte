<script module lang="ts">
    import type { ViewProject } from '$features/projects/models';

    import { SuspensionCode } from '$features/organizations/models';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import FreePlanNotification from './free-plan-notification.svelte';
    import HourlyOverageNotification from './hourly-overage-notification.svelte';
    import LoadingOrganizationNotification from './loading-organization-notification.svelte';
    import MonthlyOverageNotification from './monthly-overage-notification.svelte';
    import NoProjectsNotification from './no-projects-notification.svelte';
    import PremiumUpgradeNotification from './premium-upgrade-notification.svelte';
    import ProjectConfigurationNotification from './project-configuration-notification.svelte';
    import RequestLimitNotification from './request-limit-notification.svelte';
    import SetupFirstProjectNotification from './setup-first-project-notification.svelte';
    import SuspendedOrganizationNotification from './suspended-organization-notification.svelte';

    // Mock project data
    const mockProjects: ViewProject[] = [
        {
            created_utc: '2024-01-01T00:00:00Z',
            data: undefined,
            delete_bot_data_enabled: false,
            event_count: 150,
            has_premium_features: false,
            has_slack_integration: false,
            id: '1',
            is_configured: false,
            name: 'Frontend App',
            organization_id: 'org1',
            organization_name: 'Test Org',
            promoted_tabs: [],
            stack_count: 25,
            usage: [],
            usage_hours: []
        }
    ];

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Organizations/Notifications'
    });
</script>

<!-- Free Plan Notification -->
<Story name="Free Plan">
    <FreePlanNotification name="Acme Corporation" changePlan={() => console.log('Navigate to billing')} />
</Story>

<!-- Monthly Overage Notification -->
<Story name="Monthly Overage">
    <MonthlyOverageNotification name="Tech Startup Inc" changePlan={() => console.log('Navigate to billing')} />
</Story>

<!-- Hourly Overage Notification -->
<Story name="Hourly Overage">
    <HourlyOverageNotification name="High Traffic Corp" viewPlan={() => console.log('Navigate to usage')} />
</Story>

<!-- Request Limit Notification -->
<Story name="Request Limit Exceeded">
    <RequestLimitNotification name="API Heavy Organization" />
</Story>

<!-- Suspended Organization Notifications -->
<Story name="Suspended - Billing">
    <SuspendedOrganizationNotification
        name="Overdue Payment Corp"
        isBilling={true}
        suspensionCode={SuspensionCode.Billing}
        manageBilling={() => console.log('Navigate to billing')}
    />
</Story>

<Story name="Suspended - Abuse">
    <SuspendedOrganizationNotification name="Violation Corp" suspensionCode={SuspensionCode.Abuse} />
</Story>

<Story name="Suspended - Overage">
    <SuspendedOrganizationNotification name="Exceeded Limits Corp" suspensionCode={SuspensionCode.Overage} />
</Story>

<Story name="Suspended - Other">
    <SuspendedOrganizationNotification name="Generic Suspension Corp" suspensionCode={SuspensionCode.Other} />
</Story>

<!-- Premium Upgrade Notification -->
<Story name="Premium Upgrade">
    <PremiumUpgradeNotification name="Feature Seeker Corp" upgradePlan={() => console.log('Navigate to upgrade')} />
</Story>

<!-- Other Notifications -->
<Story name="Setup First Project">
    <SetupFirstProjectNotification />
</Story>

<Story name="No Projects">
    <NoProjectsNotification name="Fresh Start Organization" />
</Story>

<Story name="Project Configuration">
    <ProjectConfigurationNotification projects={mockProjects} />
</Story>

<Story name="Loading State">
    <LoadingOrganizationNotification />
</Story>
