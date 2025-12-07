<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import StringTrigger from './string-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/String Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <StringTrigger changed={() => {}} term="stack" value="abc123def">
            <Filter class="size-3" />
            stack:abc123def
        </StringTrigger>
        <StringTrigger changed={() => {}} term="reference" value="user-456">
            <Filter class="size-3" />
            reference:user-456
        </StringTrigger>
        <StringTrigger changed={() => {}} term="session" value="session-789">
            <Filter class="size-3" />
            session:session-789
        </StringTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <StringTrigger changed={() => {}} term="stack" value="abc123" />
        <StringTrigger changed={() => {}} term="reference" value="user-456" />
        <StringTrigger changed={() => {}} term="session" value="session-789" />
    </div>
</Story>
