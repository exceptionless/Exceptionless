<script lang="ts">
    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    import NotificationDismissButton from './notification-dismiss-button.svelte';

    interface Props extends NotificationProps {
        daysRemaining: number;
        name: string;
        onDismiss?: () => void;
        organizationId: string;
    }

    let { daysRemaining, name, onDismiss, organizationId, ...restProps }: Props = $props();

    const daysLabel = $derived(daysRemaining === 1 ? '1 day' : `${daysRemaining} days`);
</script>

<Notification variant="warning" {...restProps}>
    {#snippet action()}
        {#if onDismiss}
            <NotificationDismissButton {onDismiss} />
        {/if}
    {/snippet}
    {#if daysRemaining > 0}
        <NotificationTitle>{daysLabel} left in the {name} free trial.</NotificationTitle>
        <NotificationDescription>
            <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to keep Exie, alerts, integrations, and 90-day retention.')}>Upgrade now</A>
            to keep Exie, alerts, integrations, and 90-day retention when your trial ends.
        </NotificationDescription>
    {:else}
        <NotificationTitle>The {name} free trial has ended.</NotificationTitle>
        <NotificationDescription>
            <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to keep Exie, alerts, integrations, and 90-day retention.')}>Upgrade now</A>
            to keep Exie, alerts, integrations, and 90-day retention. This organization is moving to the Free plan.
        </NotificationDescription>
    {/if}
</Notification>
