<script lang="ts">
    import type { SimpleErrorInfo } from '$features/events/models/event-data';

    interface Props {
        error: SimpleErrorInfo;
    }

    let { error }: Props = $props();
</script>

{#if error.stack_trace}<div class="bg-inherit pl-[10px]">{error.stack_trace}</div>{/if}
