<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import type { SimpleErrorInfo } from '../../models/event-data';

    import SimpleStackTrace from './simple-stack-trace.svelte';

    const { Story } = defineMeta({
        component: SimpleStackTrace,
        tags: ['autodocs'],
        title: 'Components/Events/SimpleStackTrace'
    });

    const error: SimpleErrorInfo = {
        message: 'Unhandled Exception: ea853120-8b1c-45f9-ba05-4f02dd965269',
        stack_trace:
            '   at Dictionary<string, string> Acme.SampleAspNetCore.Controllers.ValuesController.Get() in /Acme.SampleAspNetCore/Controllers/ValuesController.cs:line 44\n   at object lambda_method56(Closure, object, object[])\n   at ValueTask<IActionResult> Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor+SyncObjectResultExecutor.Execute(ActionContext actionContext, IActionResultTypeMapper mapper, ObjectMethodExecutor executor, object controller, object[] arguments)\n   at async Task Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync()+Logged(?)\n   at async Task Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeNextActionFilterAsync()+Awaited(?)\n   at void Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Rethrow(ActionExecutedContextSealed context)\n   at Task Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(ref State next, ref Scope scope, ref object state, ref bool isCompleted)\n   at Task Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeInnerFilterAsync()\n   at async Task Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.InvokeFilterPipelineAsync()+Awaited(?)\n   at async Task Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.InvokeAsync()+Logged(?) x 2\n   at async Task Acme.AspNetCore.AcmeMiddleware.Invoke(HttpContext context) in /Acme.AspNetCore/AcmeMiddleware.cs:line 24',
        type: 'Exception'
    };

    const nestedErrors: SimpleErrorInfo = {
        inner: {
            message: 'Generated exception message.',
            stack_trace:
                '   at Dictionary<string, string> Acme.SampleAspNetCore.Controllers.ValuesController.Get() in /Acme.SampleAspNetCore/Controllers/ValuesController.cs:line 44',
            type: 'System.NullReferenceException'
        },
        message: 'Generated exception message.',
        stack_trace:
            '   at Dictionary<string, string> Acme.SampleAspNetCore.Controllers.ValuesController.Get() in /Acme.SampleAspNetCore/Controllers/ValuesController.cs:line 44\n   at object lambda_method56(Closure, object, object[])\n   at ValueTask<IActionResult> Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor+SyncObjectResultExecutor.Execute(ActionContext actionContext, IActionResultTypeMapper mapper, ObjectMethodExecutor executor, object controller, object[] arguments)\n   at async Task Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync()+Logged(?)\n   at async Task Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeNextActionFilterAsync()+Awaited(?)\n   at void Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Rethrow(ActionExecutedContextSealed context)\n   at Task Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(ref State next, ref Scope scope, ref object state, ref bool isCompleted)\n   at Task Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeInnerFilterAsync()\n   at async Task Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.InvokeFilterPipelineAsync()+Awaited(?)\n   at async Task Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.InvokeAsync()+Logged(?) x 2\n   at async Task Acme.AspNetCore.AcmeMiddleware.Invoke(HttpContext context) in /Acme.AspNetCore/AcmeMiddleware.cs:line 24',
        type: 'System.AggregateException'
    };
</script>

<Story name="Default" args={{ error: error }} />
<Story name="Nested Errors" args={{ error: nestedErrors }} />
<Story name="Empty" args={{ error: undefined }} />
