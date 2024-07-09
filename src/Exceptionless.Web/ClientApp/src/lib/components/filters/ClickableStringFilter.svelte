<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { StringFilter } from './filters';

    type Props = AProps & {
        changed: (filter: StringFilter) => void;
        term: string;
        value?: string | null;
    };
    let { changed, children, term, value, ...props }: Props = $props();

    const title = `Search ${term}:${value}`;
</script>

<A onclick={() => changed(new StringFilter(term, value ?? undefined))} {title} {...props}>
    {#if children}
        {@render children()}
    {:else}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/if}
</A>
