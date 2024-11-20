<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import IconFilter from '~icons/mdi/filter';

    import { ReferenceFilter } from './filters.svelte';

    type Props = AProps & {
        changed: (filter: ReferenceFilter) => void;
        value: string;
    };
    let { changed, class: className, value, ...props }: Props = $props();

    const title = `Search reference:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new ReferenceFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
