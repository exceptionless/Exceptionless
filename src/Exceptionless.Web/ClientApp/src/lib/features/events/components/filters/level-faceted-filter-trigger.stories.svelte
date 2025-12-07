<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import LevelTrigger from './level-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Level Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <LevelTrigger changed={() => {}} value={['error']}>
            <Filter class="size-3" />
            Error
        </LevelTrigger>
        <LevelTrigger changed={() => {}} value={['warn']}>
            <Filter class="size-3" />
            Warning
        </LevelTrigger>
        <LevelTrigger changed={() => {}} value={['info']}>
            <Filter class="size-3" />
            Info
        </LevelTrigger>
        <LevelTrigger changed={() => {}} value={['debug']}>
            <Filter class="size-3" />
            Debug
        </LevelTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <LevelTrigger changed={() => {}} value={['error']} />
        <LevelTrigger changed={() => {}} value={['warn']} />
        <LevelTrigger changed={() => {}} value={['info']} />
        <LevelTrigger changed={() => {}} value={['debug']} />
    </div>
</Story>
