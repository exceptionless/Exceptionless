<script lang="ts">
    import { createEventDispatcher } from 'svelte';

    interface Props {
        type: 'Refresh' | string;
    }

    let { type }: Props = $props();

    function onMessage({ detail }: CustomEvent<unknown>) {
        dispatch('message', detail);
    }

    $effect(() => {
        document.addEventListener(type, onMessage);

        return () => {
            document.removeEventListener(type, onMessage);
        };
    });

    const dispatch = createEventDispatcher();
</script>
