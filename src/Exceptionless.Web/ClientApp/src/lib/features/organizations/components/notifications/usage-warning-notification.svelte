<script lang="ts">
    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    import NotificationDismissButton from './notification-dismiss-button.svelte';

    interface Props extends NotificationProps {
        limit: number;
        name: string;
        onDismiss?: () => void;
        organizationId: string;
        total: number;
    }

    let { limit, name, onDismiss, organizationId, total, ...restProps }: Props = $props();

    const percent = $derived(Math.min(99, Math.floor((total / limit) * 100)));
</script>

<Notification variant="warning" {...restProps}>
    {#snippet action()}
        {#if onDismiss}
            <NotificationDismissButton {onDismiss} />
        {/if}
    {/snippet}
    <NotificationTitle>{name} has used {percent}% of its monthly events.</NotificationTitle>
    <NotificationDescription>
        {total.toLocaleString()} of {limit.toLocaleString()} events used this month. Once it reaches the limit, new events are blocked until next month.
        <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to raise your monthly event limit.')}>Upgrade now</A>
        to raise the limit.
    </NotificationDescription>
</Notification>
