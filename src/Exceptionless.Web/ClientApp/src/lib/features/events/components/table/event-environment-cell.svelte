<script lang="ts">
    import { Button } from '#comp/ui/button/index.ts';
    import EnvironmentName from '#features/projects/components/environment-name.svelte';
    import { useEnvironmentDisplayName } from '#features/projects/environment-display.svelte.ts';

    interface Props {
        changed?: (environment: string) => Promise<void> | void;
        environment?: string;
        projectId?: string;
    }
    let { changed, environment, projectId }: Props = $props();

    const display = useEnvironmentDisplayName({
        environment: () => environment,
        projectId: () => projectId
    });
</script>

{#if changed}
    <Button variant="ghost" size="xs" onclick={() => changed?.(environment ?? '')} title={`Filter by environment: ${display.name ?? 'Unspecified'}`}>
        <EnvironmentName {environment} {projectId} />
    </Button>
{:else}
    <EnvironmentName {environment} {projectId} />
{/if}
