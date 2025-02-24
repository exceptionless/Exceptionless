<script lang="ts">
    import type { ErrorInfo } from '$features/events/models/event-data';

    interface Props {
        error: ErrorInfo;
    }

    let { error }: Props = $props();
</script>

<div class="bg-inherit">
    {#if error.type}<span class="mr-1 font-bold text-purple-400">{error.type}:</span>{/if}{#if error.message}{error.message}{/if}
</div>
