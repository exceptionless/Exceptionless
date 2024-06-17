<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import type { StackStatus } from '$lib/models/api';
    import { StatusFilter } from './filters';

    type Props = AProps & {
        changed: (filter: StatusFilter) => void;
        value: StackStatus[];
    };
    let { changed, value, ...props }: Props = $props();

    const title = `Search status:${value}`;
</script>

<A on:click={() => changed(new StatusFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
