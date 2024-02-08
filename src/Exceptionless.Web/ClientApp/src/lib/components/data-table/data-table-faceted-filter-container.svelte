<script lang="ts">
    import { derived, type Readable } from 'svelte/store';

    import { Button } from '$comp/ui/button';
    import IconClose from '~icons/mdi/close';

    export let filterValues: Readable<Record<string, unknown[]>>;
    export let resetFilterValues: () => void;

    const showReset = derived(filterValues, ($filterValues) => {
        return Object.values($filterValues).some((v) => v.length > 0);
    });
</script>

<slot />

{#if $showReset}
    <Button on:click={resetFilterValues} variant="ghost" class="h-8 px-2 lg:px-3">
        Reset
        <IconClose class="ml-2 h-4 w-4" />
    </Button>
{/if}
