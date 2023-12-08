<script lang="ts">
	import type { ErrorInfo } from '$lib/models/client-data';
	import { getErrors, getStackFrame } from '$lib/helpers/persistent-event';
	import StackTraceHeader from './StackTraceHeader.svelte';
	import Code from '$comp/typography/Code.svelte';

	export let error: ErrorInfo | undefined;

	const errors = getErrors(error);
</script>

<pre class="p-2 break-words whitespace-pre-wrap border"><Code
		><StackTraceHeader {errors}
		></StackTraceHeader>{#each errors.reverse() as error, index}{#if error.stack_trace}<div
					class="pl-[10px]">{#each error.stack_trace as frame}{getStackFrame(frame)}<br
						/>{/each}{#if index < errors.length - 1}<div>--- End of inner exception stack trace ---</div>{/if}</div>{/if}{/each}</Code
	></pre>
