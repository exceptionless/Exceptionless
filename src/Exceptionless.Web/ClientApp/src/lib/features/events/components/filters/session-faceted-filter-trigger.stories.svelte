<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import SessionTrigger from './session-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Session Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <SessionTrigger changed={() => {}} value="session-abc123">
            <Filter class="size-3" />
            session:session-abc123
        </SessionTrigger>
        <SessionTrigger changed={() => {}} value="session-def456">
            <Filter class="size-3" />
            session:session-def456
        </SessionTrigger>
        <SessionTrigger changed={() => {}} value="session-ghi789">
            <Filter class="size-3" />
            session:session-ghi789
        </SessionTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <SessionTrigger changed={() => {}} value="session-abc123" />
        <SessionTrigger changed={() => {}} value="session-def456" />
        <SessionTrigger changed={() => {}} value="session-ghi789" />
    </div>
</Story>
