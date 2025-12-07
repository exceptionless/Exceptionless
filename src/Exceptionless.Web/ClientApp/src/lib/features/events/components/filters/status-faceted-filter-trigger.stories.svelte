<script lang="ts" module>
    import { StackStatus } from '$features/stacks/models';
    import Filter from '@lucide/svelte/icons/filter';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import StatusTrigger from './status-faceted-filter-trigger.svelte';

    const { Story } = defineMeta({
        tags: ['autodocs'],
        title: 'Components/Events/Filters/Status Filter Trigger'
    });
</script>

<Story name="With Text">
    <div class="flex flex-wrap gap-2 p-4">
        <StatusTrigger changed={() => {}} value={[StackStatus.Open]}>
            <Filter class="size-3" />
            Open
        </StatusTrigger>
        <StatusTrigger changed={() => {}} value={[StackStatus.Fixed]}>
            <Filter class="size-3" />
            Fixed
        </StatusTrigger>
        <StatusTrigger changed={() => {}} value={[StackStatus.Regressed]}>
            <Filter class="size-3" />
            Regressed
        </StatusTrigger>
        <StatusTrigger changed={() => {}} value={[StackStatus.Ignored]}>
            <Filter class="size-3" />
            Ignored
        </StatusTrigger>
    </div>
</Story>

<Story name="Icon Only">
    <div class="flex gap-2 p-4">
        <StatusTrigger changed={() => {}} value={[StackStatus.Open]} />
        <StatusTrigger changed={() => {}} value={[StackStatus.Fixed]} />
        <StatusTrigger changed={() => {}} value={[StackStatus.Regressed]} />
        <StatusTrigger changed={() => {}} value={[StackStatus.Ignored]} />
    </div>
</Story>
