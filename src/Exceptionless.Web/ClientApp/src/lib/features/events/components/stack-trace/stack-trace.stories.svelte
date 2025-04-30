<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import type { ErrorInfo } from '../../models/event-data';

    import StackTrace from './stack-trace.svelte';

    const { Story } = defineMeta({
        component: StackTrace,
        tags: ['autodocs'],
        title: 'Components/Events/StackTrace'
    });

    const error: ErrorInfo = {
        message: 'Unhandled Exception: db28e006-f05b-46b0-9d73-45fa89a682bc',
        stack_trace: [
            {
                column: 13,
                data: {
                    ILOffset: 229
                },
                declaring_namespace: 'Acme.SampleAspNetCore.Controllers',
                declaring_type: 'ValuesController',
                file_name: '/Acme.SampleAspNetCore/Controllers/ValuesController.cs',
                is_signature_target: true,
                line_number: 44,
                module_id: 2,
                name: 'Get'
            },
            {
                column: 0,
                data: {
                    ILOffset: -1
                },
                is_signature_target: false,
                line_number: 0,
                module_id: -1,
                name: 'lambda_method56',
                parameters: [
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        type: 'Closure',
                        type_namespace: 'System.Runtime.CompilerServices'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        type: 'Object',
                        type_namespace: 'System'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        type: 'Object[]',
                        type_namespace: 'System'
                    }
                ]
            },
            {
                column: 0,
                data: {
                    ILOffset: 11
                },
                declaring_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure',
                declaring_type: 'ActionMethodExecutor+SyncObjectResultExecutor',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 8,
                name: 'Execute',
                parameters: [
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'actionContext',
                        type: 'ActionContext',
                        type_namespace: 'Microsoft.AspNetCore.Mvc'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'mapper',
                        type: 'IActionResultTypeMapper',
                        type_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'executor',
                        type: 'ObjectMethodExecutor',
                        type_namespace: 'Microsoft.Extensions.Internal'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'controller',
                        type: 'Object',
                        type_namespace: 'System'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'arguments',
                        type: 'Object[]',
                        type_namespace: 'System'
                    }
                ]
            },
            {
                column: 0,
                data: {
                    ILOffset: 209
                },
                declaring_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure',
                declaring_type: 'ControllerActionInvoker+<<InvokeActionMethodAsync>g__Logged|12_1>d',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 8,
                name: 'MoveNext'
            },
            {
                column: 0,
                data: {
                    ILOffset: 106
                },
                declaring_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure',
                declaring_type: 'ControllerActionInvoker+<<InvokeNextActionFilterAsync>g__Awaited|10_0>d',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 8,
                name: 'MoveNext'
            },
            {
                column: 0,
                data: {
                    ILOffset: 45
                },
                declaring_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure',
                declaring_type: 'ControllerActionInvoker',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 8,
                name: 'Rethrow',
                parameters: [
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'context',
                        type: 'ActionExecutedContextSealed',
                        type_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure'
                    }
                ]
            },
            {
                column: 0,
                data: {
                    ILOffset: 922
                },
                declaring_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure',
                declaring_type: 'ControllerActionInvoker',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 8,
                name: 'Next',
                parameters: [
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'next',
                        type: 'State&',
                        type_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'scope',
                        type: 'Scope&',
                        type_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'state',
                        type: 'Object&',
                        type_namespace: 'System'
                    },
                    {
                        data: {
                            IsIn: false,
                            IsOptional: false,
                            IsOut: false
                        },
                        name: 'isCompleted',
                        type: 'Boolean&',
                        type_namespace: 'System'
                    }
                ]
            },
            {
                column: 0,
                data: {
                    ILOffset: 10
                },
                declaring_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure',
                declaring_type: 'ControllerActionInvoker',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 8,
                name: 'InvokeInnerFilterAsync'
            },
            {
                column: 0,
                data: {
                    ILOffset: 101
                },
                declaring_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure',
                declaring_type: 'ResourceInvoker+<<InvokeFilterPipelineAsync>g__Awaited|20_0>d',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 8,
                name: 'MoveNext'
            },
            {
                column: 0,
                data: {
                    ILOffset: 370
                },
                declaring_namespace: 'Microsoft.AspNetCore.Mvc.Infrastructure',
                declaring_type: 'ResourceInvoker+<<InvokeAsync>g__Logged|17_1>d',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 8,
                name: 'MoveNext'
            },
            {
                column: 17,
                data: {
                    ILOffset: 189
                },
                declaring_namespace: 'Acme.AspNetCore',
                declaring_type: 'AcmeMiddleware+<Invoke>d__3',
                file_name: '/Acme.AspNetCore/AcmeMiddleware.cs',
                is_signature_target: false,
                line_number: 24,
                module_id: 52,
                name: 'MoveNext'
            }
        ],
        type: 'Exception'
    };

    const nestedErrors: ErrorInfo = {
        inner: {
            message: 'Generated exception message.',
            stack_trace: [
                {
                    column: 0,
                    declaring_namespace: 'Acme',
                    declaring_type: 'AcmeTool',
                    is_signature_target: true,
                    line_number: 0,
                    module_id: 0,
                    name: 'SomeMethod',
                    parameters: [
                        {
                            name: 'path',
                            type: 'String'
                        }
                    ]
                },
                {
                    column: 0,
                    declaring_namespace: 'System.IO',
                    declaring_type: 'DateTime',
                    is_signature_target: false,
                    line_number: 0,
                    module_id: 0,
                    name: 'SomeMethod',
                    parameters: [
                        {
                            name: 'path',
                            type: 'String'
                        }
                    ]
                },
                {
                    column: 0,
                    declaring_namespace: 'System',
                    declaring_type: 'DateTime',
                    is_signature_target: false,
                    line_number: 0,
                    module_id: 0,
                    name: 'GenerateCode',
                    parameters: [
                        {
                            name: 'path',
                            type: 'String'
                        }
                    ]
                }
            ],
            type: 'System.NullReferenceException'
        },
        message: 'Generated exception message.',
        stack_trace: [
            {
                column: 0,
                declaring_namespace: 'System',
                declaring_type: 'DateTime',
                is_signature_target: false,
                line_number: 0,
                module_id: 0,
                name: 'GenerateCode',
                parameters: [
                    {
                        name: 'path',
                        type: 'String'
                    }
                ]
            },
            {
                column: 0,
                declaring_namespace: 'Acme.Tools',
                declaring_type: 'AcmeTool',
                is_signature_target: false,
                line_number: 0,
                module_id: 0,
                name: 'GenerateCode',
                parameters: [
                    {
                        name: 'path',
                        type: 'String'
                    }
                ]
            },
            {
                column: 0,
                declaring_namespace: 'Acme',
                declaring_type: 'AcmeTool',
                is_signature_target: false,
                line_number: 0,
                module_id: 0,
                name: 'SomeMethod',
                parameters: [
                    {
                        name: 'path',
                        type: 'String'
                    }
                ]
            },
            {
                column: 0,
                declaring_namespace: 'System',
                declaring_type: 'DateTime',
                is_signature_target: false,
                line_number: 0,
                module_id: 0,
                name: 'GenerateCode',
                parameters: [
                    {
                        name: 'path',
                        type: 'String'
                    }
                ]
            },
            {
                column: 0,
                declaring_namespace: 'System.IO',
                declaring_type: 'DateTime',
                is_signature_target: false,
                line_number: 0,
                module_id: 0,
                name: 'SomeMethod',
                parameters: [
                    {
                        name: 'path',
                        type: 'String'
                    }
                ]
            },
            {
                column: 0,
                declaring_namespace: 'Acme',
                declaring_type: 'DateTime',
                is_signature_target: false,
                line_number: 0,
                module_id: 0,
                name: 'SomeMethod',
                parameters: [
                    {
                        name: 'path',
                        type: 'String'
                    }
                ]
            }
        ],
        type: 'System.NullReferenceException'
    };
</script>

<Story name="Default" args={{ error: error }} />
<Story name="Nested Errors" args={{ error: nestedErrors }} />
<Story name="Empty" args={{ error: undefined }} />
