<script lang="ts">
    import List from '$comp/typography/List.svelte';

    export let logs: string[] | undefined;
</script>

{#if logs}
    <List items={logs} />
{/if}
