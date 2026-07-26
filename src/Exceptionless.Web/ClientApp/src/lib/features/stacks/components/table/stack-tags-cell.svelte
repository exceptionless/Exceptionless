<script lang="ts">
    import TagList from '$features/shared/components/tag-list.svelte';

    interface Props {
        onTagClick?: (tag: string) => Promise<void> | void;
        tags: string[] | undefined;
    }

    let { onTagClick, tags }: Props = $props();
</script>

<TagList maxVisible={3} {onTagClick} {tags} />
