<script lang="ts">
    import type { SimpleErrorInfo } from '$features/events/models/event-data';

    import { Code } from '$comp/typography';
    import { getErrors } from '$features/events/persistent-event';

    import StackTraceHeader from './StackTraceHeader.svelte';

    interface Props {
        error: SimpleErrorInfo;
    }

    let { error }: Props = $props();

    function cleanStackTrace(stackTrace: string) {
        return stackTrace.replace(' ', '');
    }

    const errors = getErrors(error);
</script>

<pre class="whitespace-pre-wrap break-words rounded bg-muted p-2"><Code class="px-0"
        ><StackTraceHeader {errors}></StackTraceHeader>{#each errors.reverse() as error, index}{#if error.stack_trace}<div
                    class="bg-inherit pl-[10px]">{cleanStackTrace(error.stack_trace)}</div>{#if index < errors.length - 1}<div
                        class="bg-inherit text-muted-foreground">--- End of inner error stack trace ---</div>{/if}{/if}{/each}</Code
    ></pre>
