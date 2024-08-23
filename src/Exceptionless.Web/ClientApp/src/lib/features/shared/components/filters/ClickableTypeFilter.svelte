<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import IconFilter from '~icons/mdi/filter';

    import { TypeFilter } from './filters.svelte';

    type Props = {
        changed: (filter: TypeFilter) => void;
        value: string[];
    } & AProps;
    let { changed, value, ...props }: Props = $props();

    const title = `Search type:${value}`;
</script>

<A onclick={() => changed(new TypeFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
