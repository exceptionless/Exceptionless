<script lang="ts">
    import TagList from '$features/shared/components/tag-list.svelte';

    interface Props {
        onTagClick?: (tag: string) => Promise<void> | void;
        tags?: null | string[];
    }

    let { onTagClick, tags }: Props = $props();
</script>

<TagList class="max-w-48 flex-nowrap" maxVisible={2} {onTagClick} {tags} />
