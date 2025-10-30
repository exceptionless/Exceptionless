<script lang="ts" module>
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import VersionTrigger from './version-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Version Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <VersionTrigger changed={() => {}} term="version" value="1.2.3">
            <Filter class="size-3" />
            version:1.2.3
        </VersionTrigger>
        <VersionTrigger changed={() => {}} term="version" value="2.0.0">
            <Filter class="size-3" />
            version:2.0.0
        </VersionTrigger>
        <VersionTrigger changed={() => {}} term="version" value="3.1.4">
            <Filter class="size-3" />
            version:3.1.4
        </VersionTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <VersionTrigger changed={() => {}} term="version" value="1.2.3" />
        <VersionTrigger changed={() => {}} term="version" value="2.0.0" />
        <VersionTrigger changed={() => {}} term="version" value="3.1.4" />
    </div>
</Story>
