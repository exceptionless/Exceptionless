<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import ReferenceTrigger from './reference-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Reference Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <ReferenceTrigger changed={() => {}} value="user-123">
            <Filter class="size-3" />
            reference:user-123
        </ReferenceTrigger>
        <ReferenceTrigger changed={() => {}} value="order-456">
            <Filter class="size-3" />
            reference:order-456
        </ReferenceTrigger>
        <ReferenceTrigger changed={() => {}} value="payment-789">
            <Filter class="size-3" />
            reference:payment-789
        </ReferenceTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <ReferenceTrigger changed={() => {}} value="user-123" />
        <ReferenceTrigger changed={() => {}} value="order-456" />
        <ReferenceTrigger changed={() => {}} value="payment-789" />
    </div>
</Story>
