<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import IconFilter from '~icons/mdi/filter';

    import { NumberFilter } from './filters.svelte';

    type Props = {
        changed: (filter: NumberFilter) => void;
        term: string;
        value?: number;
    } & AProps;
    let { changed, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A onclick={() => changed(new NumberFilter(term, value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
