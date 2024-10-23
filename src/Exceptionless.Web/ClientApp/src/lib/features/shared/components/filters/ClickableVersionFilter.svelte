<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import IconFilter from '~icons/mdi/filter';

    import { VersionFilter } from './filters.svelte';

    type Props = {
        changed: (filter: VersionFilter) => void;
        term: string;
        value?: string;
    } & AProps;
    let { changed, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A class="cursor-pointer" onclick={() => changed(new VersionFilter(term, value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
