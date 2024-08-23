<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import IconFilter from '~icons/mdi/filter';

    import { BooleanFilter } from './filters.svelte';

    type Props = {
        changed: (filter: BooleanFilter) => void;
        term: string;
        value?: boolean;
    } & AProps;
    let { changed, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A onclick={() => changed(new BooleanFilter(term, value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
