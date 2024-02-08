<script lang="ts">
    export let value: Date | string | undefined;
</script>

{#if value}
    {new Date(value).toLocaleString()}
{/if}
