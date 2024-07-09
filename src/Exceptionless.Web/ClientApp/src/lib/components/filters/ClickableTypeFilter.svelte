<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { TypeFilter } from './filters';

    type Props = AProps & {
        changed: (filter: TypeFilter) => void;
        value: string[];
    };
    let { changed, value, ...props }: Props = $props();

    const title = `Search type:${value}`;
</script>

<A onclick={() => changed(new TypeFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
