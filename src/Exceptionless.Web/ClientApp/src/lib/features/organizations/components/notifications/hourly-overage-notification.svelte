<script lang="ts">
    import type { NotificationProps } from '$comp/notification';

    import { resolve } from '$app/paths';
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

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
        <A href={resolve('/(app)/organization/[organizationId]/billing', { organizationId }) + '?changePlan=true'}>Upgrade now</A> to increase your limits.
    </NotificationDescription>
</Notification>
