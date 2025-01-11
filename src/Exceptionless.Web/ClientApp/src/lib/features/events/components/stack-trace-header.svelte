<script lang="ts">
    import type { ErrorInfo, SimpleErrorInfo } from '$features/events/models/event-data';

    interface Props {
        errors: (ErrorInfo | SimpleErrorInfo)[];
    }

    let { errors }: Props = $props();
</script>

{#each errors as error, index}<div class="bg-inherit">
        {#if index > 0}<span class="ml-6">---> </span>{/if}{#if error.type}<span class="mr-1 font-bold">{error.type}:</span
            >{/if}{#if error.message}{error.message}{/if}
    </div>
{/each}
