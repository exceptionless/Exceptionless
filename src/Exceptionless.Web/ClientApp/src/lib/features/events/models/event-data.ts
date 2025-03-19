export interface EnvironmentInfo {
    architecture?: string;
    available_physical_memory?: number;
    command_line?: string;
    data?: Record<string, unknown>;
    install_id?: string;
    ip_address?: string;
    machine_name?: string;
    o_s_name?: string;
    o_s_version?: string;
    process_id?: string;
    process_memory_size?: number;
    process_name?: string;
    processor_count?: number;
    runtime_version?: string;
    thread_id?: string;
    total_physical_memory?: number;
}

export interface ErrorInfo extends InnerErrorInfo {
    modules?: ModuleInfo[];
}

export interface IErrorData extends Record<string, unknown> {
    '@ext'?: Record<string, unknown>;
    '@target'?: ITargetErrorData;
}

export interface InnerErrorInfo {
    code?: string;
    data?: IErrorData;
    inner?: InnerErrorInfo;
    message?: string;
    stack_trace?: StackFrameInfo[];
    target_method?: MethodInfo;
    type?: string;
}

export interface IRequestInfoInfoData extends Record<string, unknown> {
    '@browser'?: string;
    '@browser_major_version'?: string;
    '@browser_version'?: string;
    '@device'?: string;
    '@is_bot'?: boolean;
    '@os'?: string;
    '@os_major_version'?: string;
    '@os_version'?: string;
}

export interface ISimpleErrorInfoData extends Record<string, unknown> {
    '@ext'?: Record<string, unknown>;
}

export interface ITargetErrorData extends Record<string, string | undefined> {
    ExceptionType?: string;
    Method?: string;
}

export type LogLevel = 'debug' | 'error' | 'fatal' | 'info' | 'off' | 'trace' | 'warn' | string;

export interface ManualStackingInfo {
    signature_data?: Record<string, string>;
    title?: string;
}

export interface MethodInfo {
    data?: Record<string, unknown>;
    declaring_namespace?: string;
    declaring_type?: string;
    generic_arguments?: string[];
    is_signature_target?: boolean;
    module_id?: number;
    name?: string;
    parameters?: ParameterInfo[];
}

export interface ModuleInfo {
    created_date?: Date;
    data?: Record<string, unknown>;
    is_entry?: boolean;
    modified_date?: Date;
    module_id?: number;
    name?: string;
    version?: string;
}

export interface ParameterInfo {
    data?: Record<string, unknown>;
    generic_arguments?: string[];
    name?: string;
    type?: string;
    type_namespace?: string;
}

export interface RequestInfo {
    client_ip_address?: string;
    cookies?: Record<string, string>;
    data?: IRequestInfoInfoData;
    headers?: Record<string, string[]>;
    host?: string;
    http_method?: string;
    is_secure?: boolean;
    path?: string;
    port?: number;
    post_data?: Record<string, unknown>;
    query_string?: Record<string, string>;
    referrer?: string;
    user_agent?: string;
}

export interface SimpleErrorInfo {
    data?: ISimpleErrorInfoData;
    inner?: SimpleErrorInfo;
    message?: string;
    stack_trace?: string;
    type?: string;
}

export interface StackFrameInfo extends MethodInfo {
    column?: number;
    file_name?: string;
    line_number?: number;
}

export interface UserInfo {
    data?: Record<string, unknown>;
    identity?: string;
    name?: string;
}

// TODO: Move to a helper.
export function getLogLevel(level?: LogLevel): LogLevel | null {
    switch (level?.toLowerCase().trim()) {
        case '0':
        case 'false':
        case 'no':
        case 'off':
            return 'off';
        case '1':
        case 'trace':
        case 'true':
        case 'yes':
            return 'trace';
        case 'debug':
            return 'debug';
        case 'error':
            return 'error';
        case 'fatal':
            return 'fatal';
        case 'info':
            return 'info';
        case 'warn':
            return 'warn';
        default:
            return null;
    }
}
