<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import A from '$comp/typography/A.svelte';
    import { OrganizationFilter } from './filters';

    let { value, ...props }: { value: string } = $props();
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
    <slot><IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" /></slot>
</A>
