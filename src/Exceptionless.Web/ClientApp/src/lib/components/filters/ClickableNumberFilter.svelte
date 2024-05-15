<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { NumberFilter } from './filters';

    type Props = AProps & { term: string; value?: number };
    let { term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new NumberFilter(term, value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
