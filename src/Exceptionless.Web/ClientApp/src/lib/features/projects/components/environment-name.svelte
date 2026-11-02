<script lang="ts">
    import { useEnvironmentDisplayName } from '#features/projects/environment-display.svelte.ts';

    interface Props {
        /** The environment as stored on the event. */
        environment?: null | string;
        projectId?: string;
        /** Shown when there is no environment. */
        unspecified?: string;
    }

    let { environment, projectId, unspecified = 'Unspecified' }: Props = $props();

    const display = useEnvironmentDisplayName({
        environment: () => environment,
        projectId: () => projectId
    });
</script>

{display.name ?? unspecified}
