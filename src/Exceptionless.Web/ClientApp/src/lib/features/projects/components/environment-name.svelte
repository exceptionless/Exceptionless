<script lang="ts">
    import { getProjectQuery } from '#features/projects/api.svelte.ts';
    import { getEnvironmentDisplayName } from '#features/projects/environments.ts';

    interface Props {
        /** The environment as stored on the event. */
        environment?: null | string;
        projectId?: string;
        /** Shown when there is no environment. */
        unspecified?: string;
    }

    let { environment, projectId, unspecified = 'Unspecified' }: Props = $props();

    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return environment ? projectId : undefined;
            }
        }
    });

    // Listed environments always show in their listed casing. Anything else shows the way the event reported it.
    const name = $derived(environment ? getEnvironmentDisplayName(projectQuery.data?.environments, environment) : unspecified);
</script>

{name}
