<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import { OrganizationFilter } from './filters';

    export let value: string;

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

<A on:click={onSearchClick} {title}>
    <slot />
</A>
