<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import type { ErrorInfo } from '../models/event-data';

    import StackTrace from './stack-trace.svelte';

    const { Story } = defineMeta({
        component: StackTrace,
        tags: ['autodocs'],
        title: 'Components/Events/StackTrace'
    });

    const error: ErrorInfo = {
        code: '-2146233029',
        message: 'A task was canceled.',
        stack_trace: [
            {
                column: 0,
                data: {
                    ILOffset: 372
                },
                declaring_namespace: 'Microsoft.Extensions.Diagnostics.HealthChecks',
                declaring_type: 'DefaultHealthCheckService+<CheckHealthAsync>d__4',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 56,
                name: 'MoveNext'
            },
            {
                column: 0,
                data: {
                    ILOffset: 154
                },
                declaring_namespace: 'Microsoft.AspNetCore.Diagnostics.HealthChecks',
                declaring_type: 'HealthCheckMiddleware+<InvokeAsync>d__4',
                file_name: '',
                is_signature_target: false,
                line_number: 0,
                module_id: 18,
                name: 'MoveNext'
            },
            {
                column: 0,
                data: {
                    ILOffset: 162
                },
                declaring_namespace: 'Test.AspNetCore',
                declaring_type: 'TestMiddleware+<Invoke>d__3',
                file_name: '',
                is_signature_target: true,
                line_number: 0,
                module_id: 3,
                name: 'MoveNext'
            }
        ],
        type: 'System.Threading.Tasks.TaskCanceledException'
    };

    const errorWithLineNumbers: ErrorInfo = {
        code: '-2146233088',
        message: 'Simple Exception',
        stack_trace: [
            {
                column: 17,
                data: {
                    ILOffset: 161
                },
                declaring_namespace: 'SampleAspNetCore.Controllers',
                declaring_type: 'ValuesController',
                file_name: '/Test.SampleAspNetCore/Controllers/ValuesController.cs',
                is_signature_target: true,
                line_number: 35,
                module_id: 2,
                name: 'Get'
            }
        ],
        type: 'Exception'
    };

    const nestedErrors: ErrorInfo = {
        code: '-2146233088',
        inner: {
            code: '-2147024809',
            message: 'Value does not fall within the expected range.',
            stack_trace: [
                {
                    column: 0,
                    data: {
                        ILOffset: 0
                    },
                    declaring_namespace: 'System',
                    declaring_type: 'ArgumentException',
                    file_name: '',
                    is_signature_target: false,
                    line_number: 0,
                    module_id: 1,
                    name: 'ThrowArgumentException'
                }
            ],
            type: 'System.ArgumentException'
        },
        message: 'Simple Exception',
        stack_trace: [
            {
                column: 17,
                data: {
                    ILOffset: 161
                },
                declaring_namespace: 'SampleAspNetCore.Controllers',
                declaring_type: 'ValuesController',
                file_name: '/Test.SampleAspNetCore/Controllers/ValuesController.cs',
                is_signature_target: true,
                line_number: 35,
                module_id: 2,
                name: 'Get'
            }
        ],
        type: 'Exception'
    };
</script>

<Story name="Default" args={{ error: error }} />
<Story name="Line Numbers" args={{ error: errorWithLineNumbers }} />
<Story name="Nested Errors" args={{ error: nestedErrors }} />
<Story name="Empty" args={{ error: undefined }} />
