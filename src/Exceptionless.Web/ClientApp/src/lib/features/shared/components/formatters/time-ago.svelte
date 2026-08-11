<script lang="ts">
    import Time from 'svelte-time';

    interface Props {
        value: Date | string | undefined;
    }

    const LIVE_UPDATE_INTERVAL_MS = 60_000;

    let { value }: Props = $props();
</script>

<Time live={LIVE_UPDATE_INTERVAL_MS} relative={true} timestamp={value}></Time>
