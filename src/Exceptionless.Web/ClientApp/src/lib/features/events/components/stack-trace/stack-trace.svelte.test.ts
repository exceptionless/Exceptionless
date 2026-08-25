import type { ErrorInfo } from '$features/events/models/event-data';

import { cleanup, render } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';

import StackTrace from './stack-trace.svelte';

afterEach(cleanup);

describe('StackTrace', () => {
    it('keeps frames on separate rows while allowing structured frame content to wrap', () => {
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
        const frameRows = container.querySelectorAll<HTMLElement>('[data-slot="stack-trace-frame"]');

        expect(frames?.classList.contains('flex-col')).toBe(true);
        expect(frameRows).toHaveLength(2);
        expect([...frameRows].every((frame) => frame.classList.contains('flex') && frame.classList.contains('flex-wrap'))).toBe(true);
        expect(frameRows[0]?.textContent).toContain('TransformManyBlock`2.Initialize');
        expect(frameRows[1]?.textContent).toContain('CancellationTokenSource.ThrowObjectDisposedException');
    });
});
