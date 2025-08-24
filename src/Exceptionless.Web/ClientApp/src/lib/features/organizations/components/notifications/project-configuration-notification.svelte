<script lang="ts">
    import type { ViewProject } from '$features/projects/models';

    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props {
        projects: ViewProject[];
    }

    let { projects }: Props = $props();
</script>

<Notification variant="information">
    <NotificationTitle>We haven't received any data!</NotificationTitle>
    <NotificationDescription>
        Please configure your clients for
        {#each projects as project, index (project.id)}
            {#if index > 0},
            {/if}<A href="/next/project/{project.id}/configure">{project.name}</A>
        {/each}
        {#if projects.length === 1}
            project
        {:else}
            projects
        {/if} and start becoming exceptionless in less than 60 seconds!
    </NotificationDescription>
</Notification>
