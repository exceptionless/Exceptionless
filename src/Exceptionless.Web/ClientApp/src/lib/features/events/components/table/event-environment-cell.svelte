<script lang="ts">
    import { Button } from '$comp/ui/button';

    interface Props {
        changed?: (environment: string) => Promise<void> | void;
        environment?: string;
    }
    let { changed, environment }: Props = $props();
</script>

{#if changed}
    <Button variant="ghost" size="xs" onclick={() => changed?.(environment ?? '')} title={`Filter by environment: ${environment ?? 'Unspecified'}`}>
        {environment ?? 'Unspecified'}
    </Button>
{:else}
    {environment ?? 'Unspecified'}
{/if}
