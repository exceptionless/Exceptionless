<script lang="ts">
    import type { SimpleErrorInfo } from '$features/events/models/event-data';

    interface Props {
        error: SimpleErrorInfo;
    }

    let { error }: Props = $props();
</script>

<div class="bg-inherit">
    {#if error.type}<span class="mr-1 font-bold text-purple-400">{error.type}:</span>{/if}{#if error.message}{error.message}{/if}
</div>
