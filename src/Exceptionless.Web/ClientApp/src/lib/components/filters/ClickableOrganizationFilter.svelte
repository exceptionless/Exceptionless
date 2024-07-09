<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { OrganizationFilter } from './filters';

    type Props = AProps & {
        changed: (filter: OrganizationFilter) => void;
        value: string;
    };
    let { changed, value, ...props }: Props = $props();

    const title = `Search organization:${value}`;
</script>

<A onclick={() => changed(new OrganizationFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
