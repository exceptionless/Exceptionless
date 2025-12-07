<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import TagTrigger from './tag-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Tag Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <TagTrigger changed={() => {}} value={['production']}>
            <Filter class="size-3" />
            production
        </TagTrigger>
        <TagTrigger changed={() => {}} value={['critical']}>
            <Filter class="size-3" />
            critical
        </TagTrigger>
        <TagTrigger changed={() => {}} value={['api']}>
            <Filter class="size-3" />
            api
        </TagTrigger>
        <TagTrigger changed={() => {}} value={['backend']}>
            <Filter class="size-3" />
            backend
        </TagTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <TagTrigger changed={() => {}} value={['production']} />
        <TagTrigger changed={() => {}} value={['critical']} />
        <TagTrigger changed={() => {}} value={['api']} />
    </div>
</Story>
