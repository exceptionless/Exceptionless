<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import type { ErrorInfo } from "../models/event-data";
    import StackTrace from './stack-trace.svelte';

    const { Story } = defineMeta({
        component: StackTrace,
        tags: ['autodocs'],
        title: 'Components/Events/StackTrace'
    });

    const simpleError: ErrorInfo = {
        message: "A task was canceled.",
        type: "System.Threading.Tasks.TaskCanceledException",
        code: "-2146233029",
        stack_trace: [
            {
                file_name: "",
                line_number: 0,
                column: 0,
                is_signature_target: false,
                declaring_namespace: "Microsoft.Extensions.Diagnostics.HealthChecks",
                declaring_type: "DefaultHealthCheckService+<CheckHealthAsync>d__4",
                name: "MoveNext",
                module_id: 56,
                data: {
                    ILOffset: 372
                }
            },
            {
                file_name: "",
                line_number: 0,
                column: 0,
                is_signature_target: false,
                declaring_namespace: "Microsoft.AspNetCore.Diagnostics.HealthChecks",
                declaring_type: "HealthCheckMiddleware+<InvokeAsync>d__4",
                name: "MoveNext",
                module_id: 18,
                data: {
                    ILOffset: 154
                }
            },
            {
                file_name: "",
                line_number: 0,
                column: 0,
                is_signature_target: true,
                declaring_namespace: "Test.AspNetCore",
                declaring_type: "TestMiddleware+<Invoke>d__3",
                name: "MoveNext",
                module_id: 3,
                data: {
                    ILOffset: 162
                }
            },
            {
                file_name: "",
                line_number: 0,
                column: 0,
                is_signature_target: false,
                declaring_namespace: "Serilog.AspNetCore",
                declaring_type: "RequestLoggingMiddleware+<Invoke>d__10",
                name: "MoveNext",
                module_id: 5,
                data: {
                    ILOffset: 166
                }
            }
        ]
    };
</script>

<Story name="Simple" args={{ error: simpleError }} />
<Story name="Empty" args={{ error: undefined }} />
