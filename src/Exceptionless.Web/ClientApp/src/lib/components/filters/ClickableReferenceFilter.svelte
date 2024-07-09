<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { ReferenceFilter } from './filters';

    type Props = AProps & {
        changed: (filter: ReferenceFilter) => void;
        value: string;
    };
    let { changed, value, ...props }: Props = $props();

    const title = `Search reference:${value}`;
</script>

<A onclick={() => changed(new ReferenceFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
