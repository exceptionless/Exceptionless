<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import TypeTrigger from './type-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Type Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <TypeTrigger changed={() => {}} value={['error']}>
            <Filter class="size-3" />
            Error
        </TypeTrigger>
        <TypeTrigger changed={() => {}} value={['log']}>
            <Filter class="size-3" />
            Log
        </TypeTrigger>
        <TypeTrigger changed={() => {}} value={['usage']}>
            <Filter class="size-3" />
            Usage
        </TypeTrigger>
        <TypeTrigger changed={() => {}} value={['404']}>
            <Filter class="size-3" />
            404
        </TypeTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <TypeTrigger changed={() => {}} value={['error']} />
        <TypeTrigger changed={() => {}} value={['log']} />
        <TypeTrigger changed={() => {}} value={['usage']} />
        <TypeTrigger changed={() => {}} value={['404']} />
    </div>
</Story>
