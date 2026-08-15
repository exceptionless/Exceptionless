<script lang="ts">
    import DetailSheet from './detail-sheet.svelte';

    let open = $state(false);
</script>

<button onclick={() => (open = true)}>Open details</button>
<button onclick={() => (open = false)}>Close details externally</button>
<output data-testid="detail-sheet-state">{open ? 'open' : 'closed'}</output>

<DetailSheet detailsHref="/event/abc123" onClose={() => (open = false)} {open} title="Event">
    <p>Event details</p>
</DetailSheet>
