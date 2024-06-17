<script lang="ts">
    interface Props {
        type: 'Refresh' | string;
        message: (message: unknown) => void;
    }

    let { message: message, type }: Props = $props();

    function handleMessageEvent({ detail }: CustomEvent<unknown>) {
        message(detail);
    }

    $effect(() => {
        document.addEventListener(type, handleMessageEvent);

        return () => {
            document.removeEventListener(type, handleMessageEvent);
        };
    });
</script>
