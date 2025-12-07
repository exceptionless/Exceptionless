<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import NumberTrigger from './number-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Number Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <NumberTrigger changed={() => {}} term="count" value={100}>
            <Filter class="size-3" />
            count:100
        </NumberTrigger>
        <NumberTrigger changed={() => {}} term="size" value={1024}>
            <Filter class="size-3" />
            size:1024
        </NumberTrigger>
        <NumberTrigger changed={() => {}} term="duration" value={500}>
            <Filter class="size-3" />
            duration:500
        </NumberTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <NumberTrigger changed={() => {}} term="count" value={100} />
        <NumberTrigger changed={() => {}} term="size" value={1024} />
        <NumberTrigger changed={() => {}} term="duration" value={500} />
    </div>
</Story>
