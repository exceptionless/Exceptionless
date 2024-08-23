<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import { A, type AProps } from '$comp/typography';
    import IconFilter from '~icons/mdi/filter';

    import { StatusFilter } from './filters.svelte';

    type Props = {
        changed: (filter: StatusFilter) => void;
        value: StackStatus[];
    } & AProps;
    let { changed, value, ...props }: Props = $props();

    const title = `Search status:${value}`;
</script>

<A onclick={() => changed(new StatusFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
