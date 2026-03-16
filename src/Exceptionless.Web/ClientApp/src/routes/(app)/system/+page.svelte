<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { onMount } from 'svelte';

    onMount(() => {
        goto(resolve('/(app)/system/overview'), { replaceState: true });
    });
</script>
