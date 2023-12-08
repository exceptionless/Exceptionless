<script lang="ts">
	import type { SimpleErrorInfo } from '$lib/models/client-data';
	import { getErrors } from '$lib/helpers/persistent-event';
	import StackTraceHeader from './StackTraceHeader.svelte';
	import Code from '$comp/typography/Code.svelte';

	export let error: SimpleErrorInfo | undefined;

	function cleanStackTrace(stackTrace: string) {
		return stackTrace.replace(' ', '');
	}

	const errors = getErrors(error);
</script>

<pre class="p-2 break-words whitespace-pre-wrap border"><Code
		><StackTraceHeader {errors}
		></StackTraceHeader>{#each errors.reverse() as error, index}{#if error.stack_trace}<div
					class="pl-[10px]">{cleanStackTrace(
						error.stack_trace
					)}</div>{#if index < errors.length - 1}<div>--- End of inner error stack trace ---</div>{/if}{/if}{/each}</Code
	></pre>
