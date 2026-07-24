<script lang="ts">
    import { Badge } from '$comp/ui/badge';

    interface Props {
        tags?: null | string[];
    }

    let { tags }: Props = $props();

    const visibleTags = $derived(tags?.slice(0, 2) ?? []);
    const hiddenTagCount = $derived(Math.max(0, (tags?.length ?? 0) - visibleTags.length));
    const tagList = $derived(tags?.join(', ') ?? '');
</script>

{#if visibleTags.length > 0}
    <div class="flex max-w-48 items-center gap-1" title={tagList} aria-label={`Tags: ${tagList}`}>
        {#each visibleTags as tag (tag)}
            <Badge variant="outline" class="max-w-20 truncate">{tag}</Badge>
        {/each}
        {#if hiddenTagCount > 0}
            <Badge variant="secondary">+{hiddenTagCount}</Badge>
        {/if}
    </div>
{:else}
    <span class="text-muted-foreground">—</span>
{/if}
