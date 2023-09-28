export interface EnvironmentInfo {
	processor_count?: number;
	total_physical_memory?: number;
	available_physical_memory?: number;
	command_line?: string;
	process_name?: string;
	process_id?: string;
	process_memory_size?: number;
	thread_id?: string;
	architecture?: string;
	o_s_name?: string;
	o_s_version?: string;
	ip_address?: string;
	machine_name?: string;
	install_id?: string;
	runtime_version?: string;
	data?: Record<string, unknown>;
}

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal' | string;

export interface RequestInfo {
	user_agent?: string;
	http_method?: string;
	is_secure?: boolean;
	host?: string;
	port?: number;
	path?: string;
	referrer?: string;
	client_ip_address?: string;
	headers?: Record<string, string[]>;
	cookies?: Record<string, string>;
	post_data?: Record<string, unknown>;
	query_string?: Record<string, string>;
	data?: Record<string, unknown>;
}

export interface ISimpleErrorData extends Record<string, unknown> {
	'@ext'?: Record<string, unknown>;
}

export interface SimpleError {
	message?: string;
	type?: string;
	stack_trace?: string;
	data?: ISimpleErrorData;
	inner?: SimpleError;
}

export interface ITargetErrorData extends Record<string, string | undefined> {
	ExceptionType?: string;
	Method?: string;
}

export interface IErrorData extends Record<string, unknown> {
	'@ext'?: Record<string, unknown>;
	'@target'?: ITargetErrorData;
}

export interface InnerErrorInfo {
	message?: string;
	type?: string;
	code?: string;
	data?: IErrorData;
	inner?: InnerErrorInfo;
	stack_trace?: StackFrameInfo[];
	target_method?: MethodInfo;
}

export interface ErrorInfo extends InnerErrorInfo {
	modules?: ModuleInfo[];
}

export interface MethodInfo {
	data?: Record<string, unknown>;
	generic_arguments?: string[];
	parameters?: ParameterInfo[];
	is_signature_target?: boolean;
	declaring_namespace?: string;
	declaring_type?: string;
	name?: string;
	module_id?: number;
}

export interface ParameterInfo {
	data?: Record<string, unknown>;
	generic_arguments?: string[];
	name?: string;
	type?: string;
	type_namespace?: string;
}

export interface StackFrameInfo extends MethodInfo {
	file_name?: string;
	line_number?: number;
	column?: number;
}

export interface ModuleInfo {
	data?: Record<string, unknown>;
	module_id?: number;
	name?: string;
	version?: string;
	is_entry?: boolean;
	created_date?: Date;
	modified_date?: Date;
}

export interface UserInfo {
	identity?: string;
	name?: string;
	data?: Record<string, unknown>;
}

export interface ManualStackingInfo {
	title?: string;
	signature_data?: Record<string, string>;
}
