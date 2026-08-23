<script lang="ts">
    import TagList from '$features/shared/components/tag-list.svelte';

    interface Props {
        onTagClick?: (tag: string) => Promise<void> | void;
        tags?: null | string[];
    }

    let { onTagClick, tags }: Props = $props();
</script>

<TagList
    class="max-w-48 flex-nowrap group-data-[wrap=true]/wrapped:w-full group-data-[wrap=true]/wrapped:max-w-none group-data-[wrap=true]/wrapped:flex-wrap"
    maxVisible={2}
    wrappedMaxVisible={6}
    {onTagClick}
    {tags}
/>
