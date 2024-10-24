<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import IconFilter from '~icons/mdi/filter';

    import { StringFilter } from './filters.svelte';

    type Props = {
        changed: (filter: StringFilter) => void;
        term: string;
        value?: null | string;
    } & AProps;
    let { changed, children, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A class="cursor-pointer" onclick={() => changed(new StringFilter(term, value ?? undefined))} {title} {...props}>
    {#if children}
        {@render children()}
    {:else}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/if}
</A>
