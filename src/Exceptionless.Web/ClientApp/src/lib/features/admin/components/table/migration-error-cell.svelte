<script lang="ts">
    import { Muted } from '$comp/typography';

    interface Props {
        value: null | string | undefined;
    }

    let { value }: Props = $props();
</script>

{#if value}
    <span class="text-destructive truncate font-mono text-xs" title={value}>{value}</span>
{:else}
    <Muted>—</Muted>
{/if}
