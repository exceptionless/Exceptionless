<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { SessionFilter } from './filters';

    type Props = AProps & {
        changed: (filter: SessionFilter) => void;
        value: string;
    };
    let { changed, value, ...props }: Props = $props();

    const title = `Search ref.session:${value}`;
</script>

<A on:click={() => changed(new SessionFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
