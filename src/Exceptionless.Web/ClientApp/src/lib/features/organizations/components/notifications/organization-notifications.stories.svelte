<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import type { ViewOrganization } from '#features/organizations/models.ts';
    import type { ViewProject } from '#features/projects/models.ts';

    import { SuspensionCode } from '#features/organizations/models.ts';

    import HourlyOverageNotification from './hourly-overage-notification.svelte';
    import ImpersonationNotification from './impersonation-notification.svelte';
    import MonthlyOverageNotification from './monthly-overage-notification.svelte';
    import PremiumUpgradeNotification from './premium-upgrade-notification.svelte';
    import ProjectConfigurationNotification from './project-configuration-notification.svelte';
    import RequestLimitNotification from './request-limit-notification.svelte';
    import SetupFirstProjectNotification from './setup-first-project-notification.svelte';
    import SuspendedOrganizationNotification from './suspended-organization-notification.svelte';
    import TrialEndedNotification from './trial-ended-notification.svelte';
    import TrialNotification from './trial-notification.svelte';
    import UsageWarningNotification from './usage-warning-notification.svelte';

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

    const mockOrganizations: ViewOrganization[] = [
        {
            id: 'org1',
            name: 'Test Organization'
        } as ViewOrganization
    ];

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Organizations/Notifications'
    });
</script>

<Story name="Trial Ending Soon">
    <TrialNotification daysRemaining={3} name="Acme Corporation" onDismiss={() => {}} organizationId="org1" />
</Story>
<Story name="Trial Last Day">
    <TrialNotification daysRemaining={1} name="Acme Corporation" onDismiss={() => {}} organizationId="org1" />
</Story>
<Story name="Trial Ending Today">
    <TrialNotification daysRemaining={0} name="Acme Corporation" onDismiss={() => {}} organizationId="org1" />
</Story>
<Story name="Trial Ended">
    <TrialEndedNotification maxEventsPerMonth={3000} name="Acme Corporation" onDismiss={() => {}} organizationId="org1" retentionDays={3} />
</Story>
<Story name="Usage Warning">
    <UsageWarningNotification limit={3000} name="Acme Corporation" onDismiss={() => {}} organizationId="org1" total={2550} />
</Story>
<Story name="Monthly Overage">
    <MonthlyOverageNotification name="Tech Startup Inc" organizationId="org1" />
</Story>
<Story name="Hourly Overage">
    <HourlyOverageNotification name="High Traffic Corp" organizationId="org1" />
</Story>

<Story name="Request Limit Exceeded">
    <RequestLimitNotification name="API Heavy Organization" isChatEnabled={false} openChat={() => {}} />
</Story>

<Story name="Suspended - Billing">
    <SuspendedOrganizationNotification
        name="Overdue Payment Corp"
        suspensionCode={SuspensionCode.Billing}
        isChatEnabled={false}
        openChat={() => {}}
        organizationId="org1"
    />
</Story>

<Story name="Suspended - Billing (Chat Enabled)">
    <SuspendedOrganizationNotification
        name="Overdue Payment Corp"
        suspensionCode={SuspensionCode.Billing}
        isChatEnabled={true}
        openChat={() => console.log('Chat opened for billing suspension')}
        organizationId="org1"
    />
</Story>

<Story name="Suspended - Abuse">
    <SuspendedOrganizationNotification
        name="Violation Corp"
        suspensionCode={SuspensionCode.Abuse}
        isChatEnabled={false}
        openChat={() => {}}
        organizationId="org1"
    />
</Story>

<Story name="Suspended - Overage">
    <SuspendedOrganizationNotification
        name="Exceeded Limits Corp"
        suspensionCode={SuspensionCode.Overage}
        isChatEnabled={false}
        openChat={() => {}}
        organizationId="org1"
    />
</Story>

<Story name="Suspended - Other">
    <SuspendedOrganizationNotification
        name="Generic Suspension Corp"
        suspensionCode={SuspensionCode.Other}
        isChatEnabled={false}
        openChat={() => {}}
        organizationId="org1"
    />
</Story>

<Story name="Impersonation">
    <ImpersonationNotification name="Admin User" userOrganizations={mockOrganizations} />
</Story>

<Story name="Premium Upgrade">
    <PremiumUpgradeNotification name="Feature Seeker Corp" organizationId="org1" />
</Story>

<Story name="Setup First Project">
    <SetupFirstProjectNotification />
</Story>

<Story name="Project Configuration">
    <ProjectConfigurationNotification projects={mockProjects} />
</Story>
