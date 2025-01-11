<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import IconFilter from '~icons/mdi/filter';

    import { StatusFilter } from './filters.svelte';

    type Props = AProps & {
        changed: (filter: StatusFilter) => void;
        value: StackStatus[];
    };
    let { changed, class: className, value, ...props }: Props = $props();

    const title = `Search status:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new StatusFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
