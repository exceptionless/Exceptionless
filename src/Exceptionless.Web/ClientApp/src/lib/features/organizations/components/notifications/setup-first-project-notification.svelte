<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';

    interface Props {
        addProject?: () => void; // navigate to add project
    }

    let { addProject }: Props = $props();
</script>

<Notification variant="information">
    <NotificationTitle>Setup your first project</NotificationTitle>
    <NotificationDescription>
        Please
        {#if addProject}
            <button onclick={addProject}>add a new project</button>
        {:else}
            add a new project
        {/if}
        and start becoming exceptionless in less than 60 seconds!
    </NotificationDescription>
</Notification>
