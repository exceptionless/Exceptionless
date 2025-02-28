<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import Filter from 'lucide-svelte/icons/filter';

    import { NumberFilter } from './models.svelte';

    type Props = AProps & {
        changed: (filter: NumberFilter) => void;
        term: string;
        value?: number;
    };
    let { changed, class: className, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new NumberFilter(term, value))} {title} {...props}>
    <Filter class="text-muted-foreground text-opacity-50 hover:text-primary size-5" />
</A>
