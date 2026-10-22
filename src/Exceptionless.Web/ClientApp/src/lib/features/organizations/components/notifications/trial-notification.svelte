<script lang="ts">
    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    interface Props extends NotificationProps {
        daysRemaining: number;
        name: string;
        organizationId: string;
    }

    let { daysRemaining, name, organizationId, ...restProps }: Props = $props();

    const isEndingSoon = $derived(daysRemaining <= 2);
    const daysLabel = $derived(daysRemaining === 1 ? '1 day' : `${daysRemaining} days`);
</script>

<Notification variant={isEndingSoon ? 'warning' : 'information'} {...restProps}>
    <NotificationTitle>{daysLabel} left in the {name} free trial.</NotificationTitle>
    <NotificationDescription>
        <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to keep Exie, alerts, integrations, and 90-day retention.')}>Upgrade now</A>
        to keep Exie, alerts, integrations, and 90-day retention when your trial ends.
    </NotificationDescription>
</Notification>
