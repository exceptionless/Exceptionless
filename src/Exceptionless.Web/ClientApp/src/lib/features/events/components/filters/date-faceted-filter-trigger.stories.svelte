<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import DateTrigger from './date-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Date Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <DateTrigger changed={() => {}} term="date" value="2024-01-15">
            <Filter class="size-3" />
            date:2024-01-15
        </DateTrigger>
        <DateTrigger changed={() => {}} term="created" value="2024-10-29">
            <Filter class="size-3" />
            created:2024-10-29
        </DateTrigger>
        <DateTrigger changed={() => {}} term="updated" value="2024-11-01">
            <Filter class="size-3" />
            updated:2024-11-01
        </DateTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <DateTrigger changed={() => {}} term="date" value="2024-01-15" />
        <DateTrigger changed={() => {}} term="created" value="2024-10-29" />
        <DateTrigger changed={() => {}} term="updated" value="2024-11-01" />
    </div>
</Story>
