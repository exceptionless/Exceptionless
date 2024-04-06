<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import { SessionFilter } from './filters';

    export let value: string;

    const title = `Search ref.session:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new SessionFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title}>
    <slot />
</A>
