<script lang="ts">
    import SuspensionIndicator from '../suspension-indicator.svelte';

    interface Props {
        code?: null | string;
        notes?: null | string;
    }

    let { code, notes }: Props = $props();
</script>

{#if code}
    <SuspensionIndicator {code} {notes} />
{:else}x -
{/if}
