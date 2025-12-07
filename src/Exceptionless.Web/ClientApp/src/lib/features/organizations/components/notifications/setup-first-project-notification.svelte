<script lang="ts">
    import type { NotificationProps } from '$comp/notification';

    import { resolve } from '$app/paths';
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    let { ...restProps }: NotificationProps = $props();
</script>

<Notification variant="information" {...restProps}>
    <NotificationTitle>Setup your first project</NotificationTitle>
    <NotificationDescription>
        Please
        <A href={resolve('/(app)/project/add')}>add a new project</A>
        and start becoming exceptionless in less than 60 seconds!
    </NotificationDescription>
</Notification>
