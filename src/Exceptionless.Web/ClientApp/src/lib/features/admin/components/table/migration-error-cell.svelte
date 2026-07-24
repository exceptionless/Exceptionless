<script lang="ts">
    import { Muted } from '$comp/typography';

    interface Props {
        value: null | string | undefined;
    }

    let { value }: Props = $props();
</script>

{#if value}
    <span class="truncate font-mono text-xs text-destructive" title={value}>{value}</span>
{:else}
    <Muted>—</Muted>
{/if}
