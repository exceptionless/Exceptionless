<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { ProjectFilter } from './filters';

    type Props = AProps & { organization: string; value: string[] };
    let { organization, value, ...props }: Props = $props();

    const title = `Search project:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new ProjectFilter(organization, value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
