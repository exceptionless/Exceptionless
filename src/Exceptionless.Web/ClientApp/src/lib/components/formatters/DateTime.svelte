<script lang="ts">
    interface Props {
        value: Date | string | undefined;
    }

    let { value }: Props = $props();
</script>

{#if value}
    {new Date(value).toLocaleString()}
{/if}
