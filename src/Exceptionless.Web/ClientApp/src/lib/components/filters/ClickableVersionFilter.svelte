<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import A from '$comp/typography/A.svelte';
    import { VersionFilter } from './filters';

    export let term: string;
    export let value: string | undefined;

    let className: string | undefined | null = undefined;
    export { className as class };

    const title = `Search ${term}:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new VersionFilter(term, value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} class={className}>
    <slot><IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" /></slot>
</A>
