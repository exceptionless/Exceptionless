<script lang="ts">
    import type { NotificationProps } from '$comp/notification';

    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';
    import { showUpgradeDialog } from '$features/billing/upgrade-required.svelte';

    interface Props extends NotificationProps {
        name: string;
        organizationId: string;
    }

    let { name, organizationId, ...restProps }: Props = $props();
</script>

<Notification variant="success" {...restProps}>
    <NotificationTitle>{name} is currently on a free plan.</NotificationTitle>
    <NotificationDescription>
        <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to enable premium features and extra storage.')}>Upgrade now</A>
        to enable premium features and extra storage!
    </NotificationDescription>
</Notification>
