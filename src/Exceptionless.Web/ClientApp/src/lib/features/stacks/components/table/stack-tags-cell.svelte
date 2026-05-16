<script lang="ts">
    import { Badge } from '$comp/ui/badge';

    interface Props {
        tags: string[] | undefined;
    }

    let { tags }: Props = $props();
</script>

{#if tags && tags.length > 0}
    <div class="flex flex-wrap gap-1">
        {#each tags.slice(0, 3) as tag}
            <Badge variant="outline" class="text-xs">{tag}</Badge>
        {/each}
        {#if tags.length > 3}
            <Badge variant="outline" class="text-xs">+{tags.length - 3}</Badge>
        {/if}
    </div>
{/if}
