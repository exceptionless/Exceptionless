<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';

    interface Props {
        name: string;
    }

    let { name }: Props = $props();
</script>

<Notification variant="information">
    <NotificationTitle>Setup your first project</NotificationTitle>
    <NotificationDescription>
        <strong>{name}</strong> currently has no projects. Add a new project and start becoming exceptionless in less than 60 seconds!
    </NotificationDescription>
</Notification>
