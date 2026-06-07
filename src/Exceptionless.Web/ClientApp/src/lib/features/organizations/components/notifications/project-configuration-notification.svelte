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

    const configureHref = $derived.by(() => {
        const project = projects[0];

        if (projects.length === 1 && project) {
            return resolve('/(app)/project/[projectId]/configure', { projectId: project.id });
        }

        return resolve('/(app)/project/list');
    });
</script>

<Notification variant="information" {...restProps}>
    <NotificationTitle>We haven't received any data!</NotificationTitle>
    <NotificationDescription>
        Please <A href={configureHref}>configure your clients</A> for
        {#if projects.length === 1}
            the
        {/if}
        {#each projects as project, index (project.id)}
            {#if index > 0},
            {/if}{project.name}
        {/each}
        {#if projects.length === 1}
            project
        {:else}
            projects
        {/if} and start becoming exceptionless!
    </NotificationDescription>
</Notification>
