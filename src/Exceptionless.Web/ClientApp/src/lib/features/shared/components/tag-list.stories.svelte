<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import TagList from './tag-list.svelte';

    const { Story } = defineMeta({
        component: TagList,
        tags: ['autodocs'],
        title: 'Components/Shared/TagList'
    });

    const longTags = ['environment:production', 'service:checkout-api', 'component:customer-profile-image-worker', 'release:2026.08.03'];
    const extremeLongTags = ['environment:production', 'service:checkout-api', 'component:customer-profile-image-processing-worker-with-azure-storage'];
</script>

<Story
    name="Long Tags"
    args={{
        class: 'w-64 p-4',
        tags: longTags
    }}
/>
<Story
    name="Long Tags in a Summary Cell"
    args={{
        class: 'w-48 flex-nowrap p-4',
        maxVisible: 2,
        tags: longTags
    }}
/>
<Story
    name="Clickable Long Tags in a Summary Cell"
    args={{
        class: 'w-48 flex-nowrap p-4',
        maxVisible: 2,
        onTagClick: () => {},
        tags: longTags
    }}
/>
<Story
    name="Extreme Long Tag in Overflow Popover"
    args={{
        class: 'w-48 flex-nowrap p-4',
        maxVisible: 2,
        onTagClick: () => {},
        tags: extremeLongTags
    }}
/>
<Story
    name="Clickable Long Tags"
    args={{
        class: 'w-64 p-4',
        onTagClick: () => {},
        tags: longTags
    }}
/>
