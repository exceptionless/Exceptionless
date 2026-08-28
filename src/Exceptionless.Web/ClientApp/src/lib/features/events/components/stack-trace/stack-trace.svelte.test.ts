import type { ErrorInfo, SimpleErrorInfo } from '$features/events/models/event-data';

import { cleanup, render } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';

import SimpleStackTrace from '../simple-stack-trace/simple-stack-trace.svelte';
import StackTrace from './stack-trace.svelte';

afterEach(cleanup);

function expectHorizontalStackTraceScroll(container: HTMLElement) {
    const stackTrace = container.querySelector('pre');

    expect(stackTrace).not.toBeNull();
    expect(stackTrace?.classList).toContain('max-w-full');
    expect(stackTrace?.classList).toContain('overflow-x-auto');
    expect(stackTrace?.classList).toContain('whitespace-pre');
    expect(stackTrace?.classList).not.toContain('whitespace-pre-wrap');
    expect(stackTrace?.classList).not.toContain('wrap-break-word');
}

describe('StackTrace', () => {
    it('keeps structured stack frames on separate unwrapped rows with horizontal scrolling', () => {
        const error: ErrorInfo = {
            message: 'The CancellationTokenSource has been disposed.',
            stack_trace: [
                {
                    data: { ILOffset: 363 },
                    declaring_namespace: 'System.Threading.Tasks.Dataflow',
                    declaring_type: 'TransformManyBlock`2',
                    name: 'Initialize',
                    parameters: [
                        {
                            name: 'processMessageAction',
                            type: 'Action`1',
                            type_namespace: 'System'
                        },
                        {
                            name: 'dataflowBlockOptions',
                            type: 'ExecutionDataflowBlockOptions',
                            type_namespace: 'System.Threading.Tasks.Dataflow'
                        }
                    ]
                },
                {
                    declaring_namespace: 'System.Threading',
                    declaring_type: 'CancellationTokenSource',
                    name: 'ThrowObjectDisposedException'
                }
            ],
            type: 'ObjectDisposedException'
        };

        const { container } = render(StackTrace, { error });
        const frames = container.querySelector<HTMLElement>('[data-slot="stack-trace-frames"]');
        const frameContents = container.querySelectorAll<HTMLElement>('[data-slot="stack-trace-frame-content"]');
        const frameRows = container.querySelectorAll<HTMLElement>('[data-slot="stack-trace-frame"]');

        expectHorizontalStackTraceScroll(container);
        expect(frames?.classList).toContain('flex-col');
        expect(frameRows).toHaveLength(2);
        expect(frameContents).toHaveLength(2);
        expect([...frameRows].every((frame) => !frame.classList.contains('flex-wrap'))).toBe(true);
        expect([...frameContents].every((frame) => frame.classList.contains('whitespace-nowrap'))).toBe(true);
        expect(frameContents[0]?.textContent).toContain('at System.Threading.Tasks.Dataflow.TransformManyBlock`2.Initialize');
        expect(frameContents[1]?.textContent).toContain('at System.Threading.CancellationTokenSource.ThrowObjectDisposedException');
    });

    it('keeps simple stack frames on one line with horizontal scrolling', () => {
        const error: SimpleErrorInfo = {
            message: 'Simple stack trace',
            stack_trace: 'at Example.Run(String value) in /src/Example.cs:line 42',
            type: 'System.Exception'
        };

        const { container } = render(SimpleStackTrace, { error });

        expectHorizontalStackTraceScroll(container);
    });
});
