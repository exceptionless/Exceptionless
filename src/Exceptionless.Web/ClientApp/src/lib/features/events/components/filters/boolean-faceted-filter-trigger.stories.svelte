<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import BooleanTrigger from './boolean-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Boolean Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <BooleanTrigger changed={() => {}} term="fixed" value={true}>
            <Filter class="size-3" />
            Fixed: true
        </BooleanTrigger>
        <BooleanTrigger changed={() => {}} term="hidden" value={false}>
            <Filter class="size-3" />
            Hidden: false
        </BooleanTrigger>
        <BooleanTrigger changed={() => {}} term="critical" value={true}>
            <Filter class="size-3" />
            Critical: true
        </BooleanTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <BooleanTrigger changed={() => {}} term="fixed" value={true} />
        <BooleanTrigger changed={() => {}} term="hidden" value={false} />
        <BooleanTrigger changed={() => {}} term="critical" value={true} />
    </div>
</Story>
