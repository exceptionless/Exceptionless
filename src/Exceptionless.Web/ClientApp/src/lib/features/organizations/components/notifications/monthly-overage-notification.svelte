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
    <NotificationTitle>{name} has reached its monthly plan limit.</NotificationTitle>
    <NotificationDescription>
        <A onclick={() => showUpgradeDialog(organizationId, 'Upgrade to continue receiving events.')}>Upgrade now</A>
        to continue receiving events.
    </NotificationDescription>
</Notification>
