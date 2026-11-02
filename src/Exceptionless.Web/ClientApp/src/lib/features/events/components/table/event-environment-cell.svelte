<script lang="ts">
    import { Button } from '#comp/ui/button/index.ts';
    import EnvironmentName from '#features/projects/components/environment-name.svelte';

    interface Props {
        changed?: (environment: string) => Promise<void> | void;
        environment?: string;
        projectId?: string;
    }
    let { changed, environment, projectId }: Props = $props();
</script>

{#if changed}
    <Button variant="ghost" size="xs" onclick={() => changed?.(environment ?? '')} title={`Filter by environment: ${environment ?? 'Unspecified'}`}>
        <EnvironmentName {environment} {projectId} />
    </Button>
{:else}
    <EnvironmentName {environment} {projectId} />
{/if}
