<script lang="ts">
    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    import NotificationDismissButton from './notification-dismiss-button.svelte';

    interface Props extends NotificationProps {
        maxEventsPerMonth?: number;
        name: string;
        onDismiss?: () => void;
        organizationId: string;
        retentionDays?: number;
    }

    let { maxEventsPerMonth, name, onDismiss, organizationId, retentionDays, ...restProps }: Props = $props();

    const hasLimits = $derived(typeof maxEventsPerMonth === 'number' && maxEventsPerMonth > 0 && typeof retentionDays === 'number');
</script>

<Notification variant="information" {...restProps}>
    {#snippet action()}
        {#if onDismiss}
            <NotificationDismissButton {onDismiss} />
        {/if}
    {/snippet}
    <NotificationTitle>Your trial has ended. {name} is now on the Free plan.</NotificationTitle>
    <NotificationDescription>
        {#if hasLimits}
            The Free plan includes {maxEventsPerMonth?.toLocaleString()} events per month and {retentionDays} days of data retention, without Exie, alerts, or integrations.
        {:else}
            The Free plan doesn't include Exie, alerts, or integrations.
        {/if}
        <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to get Exie, alerts, integrations, and longer retention back.')}>Upgrade now</A>
        to get them back.
    </NotificationDescription>
</Notification>
