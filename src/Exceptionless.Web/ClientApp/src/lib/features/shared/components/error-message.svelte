<script lang="ts">
    import P from './typography/p.svelte';

    interface Props {
        message?: string | string[];
    }

    let { message }: Props = $props();

    const text = $derived(Array.isArray(message) ? message.join(', ') : message);
</script>

{#if text}
    <P class="text-[0.8rem] font-medium text-destructive">{text}</P>
{/if}
