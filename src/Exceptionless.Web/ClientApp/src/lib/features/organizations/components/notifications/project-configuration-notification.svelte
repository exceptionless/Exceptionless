<script lang="ts">
    import type { NotificationProps } from '$comp/notification';
    import type { ViewProject } from '$features/projects/models';

    import { resolve } from '$app/paths';
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props extends NotificationProps {
        projects: ViewProject[];
    }

    let { projects, ...restProps }: Props = $props();
</script>

<Notification variant="information" {...restProps}>
    <NotificationTitle>We haven't received any data!</NotificationTitle>
    <NotificationDescription>
        Please configure your clients for
        {#each projects as project, index (project.id)}
            {#if index > 0},
            {/if}<A href={resolve('/(app)/project/[projectId]/configure', { projectId: project.id })}>{project.name}</A>
        {/each}
        {#if projects.length === 1}
            project
        {:else}
            projects
        {/if} and start becoming exceptionless in less than 60 seconds!
    </NotificationDescription>
</Notification>
