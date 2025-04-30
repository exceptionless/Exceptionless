<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import Filter from '@lucide/svelte/icons/filter';

    import { VersionFilter } from './models.svelte';

    type Props = AProps & {
        changed: (filter: VersionFilter) => void;
        term: string;
        value?: string;
    };
    let { changed, class: className, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new VersionFilter(term, value))} {title} {...props}>
    <Filter class="text-muted-foreground text-opacity-50 hover:text-primary size-5" />
</A>
