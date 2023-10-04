<script lang="ts">
	import type { ErrorInfo, ParameterInfo, StackFrameInfo } from '$lib/models/client-data';

	export let error: ErrorInfo | undefined;

	import { onMount } from 'svelte';
	import DOMPurify from 'dompurify';
	import { getErrors } from '$lib/helpers/persistent-event';

	let textStackTrace: string;
	let stackTrace: string;

	function buildParameter(parameter: ParameterInfo) {
		let result = '';

		const parts = [];
		if (parameter.type_namespace) {
			parts.push(parameter.type_namespace);
		}

		if (parameter.type) {
			parts.push(parameter.type);
		}

		result += parts.join('.').replace('+', '.');

		if (parameter.generic_arguments && parameter.generic_arguments.length > 0) {
			result += `[${parameter.generic_arguments.join(',')}]`;
		}

		if (parameter.name) {
			result += ` ${parameter.name}`;
		}

		return result;
	}

	function buildParameters(parameters?: ParameterInfo[]) {
		let result = '(';

		parameters?.forEach((parameter, index) => {
			if (index > 0) {
				result += ', ';
			}

			result += buildParameter(parameter);
		});

		return result + ')';
	}

	function buildStackFrame(frame: StackFrameInfo, includeHTML: boolean) {
		if (!frame) {
			return '<null>\r\n';
		}

		const typeNameParts = [];
		if (frame.declaring_namespace) {
			typeNameParts.push(frame.declaring_namespace);
		}

		if (frame.declaring_type) {
			typeNameParts.push(frame.declaring_type);
		}

		typeNameParts.push(frame.name || '<anonymous>');

		let result = `at ${typeNameParts.join('.').replace('+', '.')}`;

		if (frame.generic_arguments && frame.generic_arguments.length > 0) {
			result += `[${frame.generic_arguments.join(',')}]`;
		}

		result += buildParameters(frame.parameters);
		if (frame.data && (frame.data.ILOffset || frame.data.NativeOffset)) {
			result += ` at offset ${frame.data.ILOffset || frame.data.NativeOffset}`;
		}

		if (frame.file_name) {
			result += ` in ${frame.file_name}`;
			if (frame.line_number) {
				result += `:line ${frame.line_number}`;
			}

			if (frame.column) {
				result += `:col ${frame.column}`;
			}
		}

		if (includeHTML) {
			return escapeHTML(`${result}\r\n`);
		}

		return `${result}\r\n`;
	}

	function buildStackFrames(errors: ErrorInfo[], includeHTML: boolean) {
		let frames = '';
		errors.forEach((error, index) => {
			const stackTrace = error.stack_trace;
			if (stackTrace) {
				if (includeHTML) {
					frames += '<div class="stack-frame">';
				}

				stackTrace.forEach((trace) => {
					frames += buildStackFrame(trace, includeHTML);
				});

				if (index < errors.length - 1) {
					frames += includeHTML
						? '<div>--- End of inner exception stack trace ---</div>'
						: '--- End of inner exception stack trace ---';
				}

				if (includeHTML) {
					frames += '</div>';
				}
			}
		});

		return frames;
	}

	function buildStackTrace(errors: ErrorInfo[], includeHTML: boolean) {
		return (
			buildStackTraceHeader(errors, includeHTML) +
			buildStackFrames(errors.reverse(), includeHTML)
		);
	}

	function buildStackTraceHeader(errors: ErrorInfo[], includeHTML: boolean) {
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
	});
</script>

<pre
	class="max-h-[500px] overflow-y-scroll overflow-x-scroll break-normal resize-y whitespace-pre tab-size-2"><code
		>{@html stackTrace}</code
	></pre>
