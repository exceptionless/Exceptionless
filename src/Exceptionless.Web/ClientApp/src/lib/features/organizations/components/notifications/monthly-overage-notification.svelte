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

<Notification variant="destructive" {...restProps}>
    <NotificationTitle>{name} has reached its monthly plan limit.</NotificationTitle>
    <NotificationDescription>
        <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to continue receiving events.')}>Upgrade now</A>
        to continue receiving events.
    </NotificationDescription>
</Notification>
