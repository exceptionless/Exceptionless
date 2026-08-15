<script lang="ts">
    import DetailSheet from './detail-sheet.svelte';

    let historyValue = $state<string>();
</script>

<button onclick={() => (historyValue = 'abc123')}>Open details</button>
<button onclick={() => (historyValue = undefined)}>Close details externally</button>
<button onclick={() => (historyValue = 'def456')}>Navigate within details</button>
<output data-testid="detail-sheet-state">{historyValue ? `open:${historyValue}` : 'closed'}</output>

<DetailSheet
    detailsHref="/event/abc123"
    historyKey="event"
    {historyValue}
    onClose={() => (historyValue = undefined)}
    onOpen={(value) => (historyValue = value)}
    open={!!historyValue}
    title="Event"
>
    <p>Event details</p>
</DetailSheet>
