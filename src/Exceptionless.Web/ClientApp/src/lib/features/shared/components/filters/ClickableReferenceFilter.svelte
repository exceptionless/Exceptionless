<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import IconFilter from '~icons/mdi/filter';

    import { ReferenceFilter } from './filters.svelte';

    type Props = {
        changed: (filter: ReferenceFilter) => void;
        value: string;
    } & AProps;
    let { changed, value, ...props }: Props = $props();

    const title = `Search reference:${value}`;
</script>

<A class="cursor-pointer" onclick={() => changed(new ReferenceFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
