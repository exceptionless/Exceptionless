<script lang="ts">
    import type { Stack } from '$lib/generated/api';

    import ProjectLogLevel from '$features/projects/components/project-log-level.svelte';

    interface Props {
        stack: Stack;
    }

    let { stack }: Props = $props();

    const source = $derived(stack.signature_info?.['Source'] ?? '');
</script>

{#if stack.type === 'log' && source}
    <ProjectLogLevel projectId={stack.project_id} {source} />
{/if}
