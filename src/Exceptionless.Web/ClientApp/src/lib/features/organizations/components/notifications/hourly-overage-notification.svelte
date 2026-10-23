<script lang="ts">
    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    interface Props extends NotificationProps {
        name: string;
        organizationId: string;
    }

    let { name, organizationId, ...restProps }: Props = $props();
</script>

<Notification variant="destructive" {...restProps}>
    <NotificationTitle>Events are currently being throttled for {name}</NotificationTitle>
    <NotificationDescription>
        Events are currently being throttled to prevent using up your plan limit in a small window of time.
        <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to increase your hourly event limits.')}>Upgrade now</A> to increase your limits.
    </NotificationDescription>
</Notification>
