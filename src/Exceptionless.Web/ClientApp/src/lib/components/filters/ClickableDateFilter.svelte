<script lang="ts">
    import type { HTMLAnchorAttributes } from 'svelte/elements';

    import IconFilter from '~icons/mdi/filter';
    import A from '$comp/typography/A.svelte';
    import { DateFilter } from './filters';

    type Props = HTMLAnchorAttributes & { term: string; value?: boolean };
    let { term, value, ...props }: { term: string; value: Date | string | undefined } = $props();
    const title = `Search ${term}:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new DateFilter(term, value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} {...props}>
    <slot><IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" /></slot>
</A>
