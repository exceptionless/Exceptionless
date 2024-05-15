<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import A from '$comp/typography/A.svelte';
    import type { StackStatus } from '$lib/models/api.generated';
    import { StatusFilter } from './filters';

    let { value, ...props }: { value: StackStatus[] } = $props();
    const title = `Search status:${value}`;

    function onSearchClick(e: Event) {
        e.preventDefault();
        document.dispatchEvent(
            new CustomEvent('filter', {
                detail: new StatusFilter(value)
            })
        );
    }
</script>

<A on:click={onSearchClick} {title} {...props}>
    <slot><IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" /></slot>
</A>
