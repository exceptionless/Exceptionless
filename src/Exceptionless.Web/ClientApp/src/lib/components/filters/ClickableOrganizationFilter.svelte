<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { OrganizationFilter } from './filters';

    type Props = AProps & { value: string };
    let { value, ...props }: Props = $props();

    const title = `Search organization:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new OrganizationFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
