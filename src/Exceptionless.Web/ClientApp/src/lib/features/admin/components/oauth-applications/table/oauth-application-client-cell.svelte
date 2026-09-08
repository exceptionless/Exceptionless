<script lang="ts">
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';

    interface Props {
        clientId: string;
    }

    let { clientId }: Props = $props();
</script>

<div class="flex min-w-0 items-center gap-1">
    <code class="bg-muted min-w-0 truncate rounded px-1.5 py-0.5 text-xs" title={clientId}>{clientId}</code>
    <CopyToClipboardButton value={clientId} variant="ghost" size="icon-sm" />
</div>
