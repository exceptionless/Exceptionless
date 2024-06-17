<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { BooleanFilter } from './filters';

    type Props = AProps & {
        changed: (filter: BooleanFilter) => void;
        term: string;
        value?: boolean;
    };
    let { changed, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A on:click={() => changed(new BooleanFilter(term, value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
