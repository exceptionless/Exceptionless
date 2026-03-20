<script lang="ts">
    import { onMount } from 'svelte';

    interface Props {
        onMountProbe: () => void;
    }

    let { onMountProbe }: Props = $props();

    onMount(() => {
        onMountProbe();
    });
</script>

<div data-testid="mount-probe"></div>