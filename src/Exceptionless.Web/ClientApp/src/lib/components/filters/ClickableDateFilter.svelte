<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { DateFilter } from './filters';

    type Props = AProps & {
        changed: (filter: DateFilter) => void;
        term: string;
        value?: Date | string;
    };
    let { changed, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A on:click={() => changed(new DateFilter(term, value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
