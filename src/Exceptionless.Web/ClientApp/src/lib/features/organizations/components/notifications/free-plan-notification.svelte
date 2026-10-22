<script lang="ts">
    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    interface Props extends NotificationProps {
        maxEventsPerMonth: number;
        name: string;
        organizationId: string;
        retentionDays: number;
        trialEnded?: boolean;
    }

    let { maxEventsPerMonth, name, organizationId, retentionDays, trialEnded = false, ...restProps }: Props = $props();
</script>

<Notification variant="success" {...restProps}>
    <NotificationTitle>
        {#if trialEnded}
            Your trial has ended. {name} is now on the Free plan.
        {:else}
            {name} is currently on a free plan.
        {/if}
    </NotificationTitle>
    <NotificationDescription>
        The Free plan includes {maxEventsPerMonth.toLocaleString()} events per month and {retentionDays} days of data retention, without Exie, alerts, or integrations.
        <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to enable premium features and extra storage.')}>Upgrade now</A>
        to get them back.
    </NotificationDescription>
</Notification>
