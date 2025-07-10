<script lang="ts">
    interface Props {
        value: boolean | null | undefined;
    }

    let { value }: Props = $props();
</script>

{#if value === true}
    Yes
{:else}
    No
{/if}
