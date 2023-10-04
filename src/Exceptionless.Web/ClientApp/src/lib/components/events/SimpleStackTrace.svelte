<script lang="ts">
	import type { SimpleErrorInfo } from '$lib/models/client-data';

	export let error: SimpleErrorInfo | undefined;

	import { onMount } from 'svelte';
	import { getErrors } from '$lib/helpers/persistent-event';
	import DOMPurify from 'dompurify';

	let textStackTrace: string;
	let stackTrace: string;

	function buildStackFrames(errors: SimpleErrorInfo[], includeHTML: boolean) {
		let frames = '';
		errors.forEach((error, index) => {
			const stackTrace = error.stack_trace;
			if (stackTrace) {
				if (includeHTML) {
					frames += `<div class="pl-[10px]">${escapeHTML(stackTrace.replace(' ', ''))}`;

					if (index < errors.length - 1) {
						frames += '<div>--- End of inner error stack trace ---</div>';
					}

					frames += '</div>';
				} else {
					frames += stackTrace.replace(' ', '');

					if (index < errors.length - 1) {
						frames += '--- End of inner error stack trace ---';
					}
				}
			}
		});

		return frames;
	}

	function buildStackTrace(errors: SimpleErrorInfo[], includeHTML: boolean) {
		return (
			buildStackTraceHeader(errors, includeHTML) +
			buildStackFrames(errors.reverse(), includeHTML)
		);
	}

	function buildStackTraceHeader(errors: SimpleErrorInfo[], includeHTML: boolean) {
		let header = '';
		errors.forEach((error, index) => {
			if (includeHTML) {
				header += '<span class="block">';
			}

			if (index > 0) {
				header += ' ---> ';
			}

			const hasType = !!error.type;
			if (hasType) {
				if (includeHTML) {
					header += `<span class="font-bold">${escapeHTML(error.type)}</span>: `;
				} else {
					header += `${error.type}: `;
				}
			}

			if (error.message) {
				if (includeHTML) {
					header += escapeHTML(error.message);
				} else {
					header += error.message;
				}
			}

			if (hasType) {
				if (includeHTML) {
					header += '</span>';
				} else {
					header += '\r\n';
				}
			}
		});

		return header;
	}

	function escapeHTML(input?: string) {
		if (!input) {
			return input;
		}

		return DOMPurify.sanitize(
			input
				.replace(/&/g, '&amp;')
				.replace(/</g, '&lt;')
				.replace(/>/g, '&gt;')
				.replace(/"/g, '&quot;')
				.replace(/'/g, '&#039;')
		);
	}

	onMount(() => {
		const errors = getErrors(error);
		stackTrace = buildStackTrace(errors, true);
		textStackTrace = buildStackTrace(errors, false);
		console.log({ stackTrace, textStackTrace });
	});
</script>

<pre
	class="max-h-[500px] overflow-y-scroll overflow-x-scroll break-normal resize-y whitespace-pre tab-size-2"><code
		>{@html stackTrace}</code
	></pre>
