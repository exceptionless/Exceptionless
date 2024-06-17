<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { StringFilter } from './filters';

    type Props = AProps & {
        changed: (filter: StringFilter) => void;
        term: string;
        value?: string | null;
    };
    let { changed, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A on:click={() => changed(new StringFilter(term, value ?? undefined))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
