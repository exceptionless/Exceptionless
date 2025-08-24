<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';
</script>

<Notification variant="information">
    <NotificationTitle>Setup your first project</NotificationTitle>
    <NotificationDescription>
        Please
        <A href="/next/project/add">add a new project</A>
        and start becoming exceptionless in less than 60 seconds!
    </NotificationDescription>
</Notification>
