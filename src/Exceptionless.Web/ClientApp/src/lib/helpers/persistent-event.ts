import type { PersistentEvent } from '$lib/models/api';
import type {
	ErrorInfo,
	ParameterInfo,
	SimpleErrorInfo,
	StackFrameInfo
} from '$lib/models/client-data';
import { buildUrl } from './url';

export function getLocation(event: PersistentEvent) {
	const location = event.data?.['@location'];
	if (!location) {
		return null;
	}

	return [location.locality, location.level1, location.country]
		.filter((value) => value?.length)
		.reduce((a, b, index) => {
			a += (index > 0 ? ', ' : '') + b;
			return a;
		}, '');
}

export function getRequestInfoUrl(event: PersistentEvent) {
	const requestInfo = event.data?.['@request'];
	if (requestInfo) {
		return buildUrl(
			requestInfo.is_secure,
			requestInfo.host,
			requestInfo.port,
			requestInfo.path,
			requestInfo.query_string
		);
	}

	return null;
}

export function getMessage(event: PersistentEvent) {
	const error = event.data?.['@error'];
	if (error) {
		return getTargetInfoMessage(error) || event.message;
	}

	return event.message;
}

export function getErrors<T extends SimpleErrorInfo | ErrorInfo>(error: T | undefined): T[] {
	const errors: T[] = [];
	let current: T | undefined = error;
	while (current) {
		errors.push(current);
		current = current?.inner as T | undefined;
	}

	return errors;
}

export function getErrorType(event: PersistentEvent) {
	const error = event.data?.['@error'];
	if (error) {
		const type = getTargetInfoExceptionType(error);
		return type || error.type || 'Unknown';
	}

	const simpleError = event.data?.['@simple_error'];
	return simpleError?.type || 'Unknown';
}

export function getTargetInfo(error: ErrorInfo) {
	return error.data?.['@target'];
}

export function getTargetInfoExceptionType(error: ErrorInfo) {
	const target = getTargetInfo(error);
	return target?.ErrorType;
}

export function getTargetInfoMethod(error: ErrorInfo) {
	const target = getTargetInfo(error);
	return target?.Method;
}

export function getTargetInfoMessage(error: ErrorInfo) {
	const target = getTargetInfo(error);
	return target?.Message;
}

function getParameter(parameter: ParameterInfo) {
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

function getParameters(parameters?: ParameterInfo[]) {
	let result = '(';

	parameters?.forEach((parameter, index) => {
		if (index > 0) {
			result += ', ';
		}

		result += getParameter(parameter);
	});

	return result + ')';
}

export function getStackFrame(frame: StackFrameInfo) {
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

	result += getParameters(frame.parameters);
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

	return result;
}

function getStackTraceHeader<T extends SimpleErrorInfo | ErrorInfo>(errors: T[]) {
	let header = '';
	errors.forEach((error, index) => {
		if (index > 0) {
			header += ' ---> ';
		}

		const hasType = !!error.type;
		if (hasType) {
			header += `${error.type}: `;
		}

		if (error.message) {
			header += error.message;
		}

		if (hasType) {
			header += '\r\n';
		}
	});

	return header;
}

export function getErrorInfoStackTrace(error: ErrorInfo) {
	function buildStackFrames(errors: ErrorInfo[]) {
		let frames = '';
		errors.forEach((error, index) => {
			const stackTrace = error.stack_trace;
			if (stackTrace) {
				stackTrace.forEach((trace) => {
					frames += `${getStackFrame(trace)}\r\n`;
				});

				if (index < errors.length - 1) {
					frames += '--- End of inner exception stack trace ---';
				}
			}
		});

		return frames;
	}

	const errors = getErrors(error);
	return getStackTraceHeader(errors) + buildStackFrames(errors.reverse());
}

export function getSimpleErrorInfoStackTrace(error: SimpleErrorInfo) {
	function buildStackFrames(errors: SimpleErrorInfo[]) {
		let frames = '';
		errors.forEach((error, index) => {
			const stackTrace = error.stack_trace;
			if (stackTrace) {
				frames += stackTrace.replace(' ', '');

				if (index < errors.length - 1) {
					frames += '--- End of inner error stack trace ---';
				}
			}
		});

		return frames;
	}

	const errors = getErrors(error);
	return getStackTraceHeader(errors) + buildStackFrames(errors.reverse());
}
