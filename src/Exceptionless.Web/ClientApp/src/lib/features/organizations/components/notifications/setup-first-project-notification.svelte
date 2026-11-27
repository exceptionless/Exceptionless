<script lang="ts">
    import { resolve } from '$app/paths';

    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';

    let { ...restProps }: NotificationProps = $props();
</script>

<Notification variant="information" {...restProps}>
    <NotificationTitle>Set up your first project</NotificationTitle>
    <NotificationDescription>
        <A href={resolve('/(app)/project/add')}>Add a new project</A>
        and start becoming Exceptionless in less than 60 seconds!
    </NotificationDescription>
</Notification>
