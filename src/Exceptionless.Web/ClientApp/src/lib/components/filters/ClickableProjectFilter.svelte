<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import { ProjectFilter } from './filters';

    export let organization: string;
    export let value: string[];

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

<A on:click={onSearchClick} {title}>
    <slot />
</A>
