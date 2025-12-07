<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import ProjectTrigger from './project-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Project Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <ProjectTrigger changed={() => {}} value={['proj123']}>
            <Filter class="size-3" />
            Web Application
        </ProjectTrigger>
        <ProjectTrigger changed={() => {}} value={['proj456']}>
            <Filter class="size-3" />
            Mobile App
        </ProjectTrigger>
        <ProjectTrigger changed={() => {}} value={['proj789']}>
            <Filter class="size-3" />
            API Service
        </ProjectTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <ProjectTrigger changed={() => {}} value={['proj123']} />
        <ProjectTrigger changed={() => {}} value={['proj456']} />
        <ProjectTrigger changed={() => {}} value={['proj789']} />
    </div>
</Story>
