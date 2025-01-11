<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import Filter from 'lucide-svelte/icons/filter';

    import { BooleanFilter } from './filters.svelte';

    type Props = AProps & {
        changed: (filter: BooleanFilter) => void;
        term: string;
        value?: boolean;
    };
    let { changed, class: className, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new BooleanFilter(term, value))} {title} {...props}>
    {#snippet children()}
        <Filter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
