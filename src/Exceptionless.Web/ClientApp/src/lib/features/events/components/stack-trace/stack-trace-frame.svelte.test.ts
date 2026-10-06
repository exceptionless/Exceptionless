import type { StackFrameInfo } from '$features/events/models/event-data';

import { render } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import StackTraceFrame from './stack-trace-frame.svelte';

const frame: StackFrameInfo = {
    column: 7,
    data: {
        ILOffset: 12
    },
    declaring_namespace: 'Acme',
    declaring_type: 'Widget+Nested',
    file_name: '/src/widget.cs',
    generic_arguments: ['T'],
    line_number: 42,
    name: 'Run',
    parameters: [
        {
            name: 'name',
            type: 'String',
            type_namespace: 'System'
        },
        {
            generic_arguments: ['Item'],
            name: 'items',
            type: 'List',
            type_namespace: 'System.Collections.Generic'
        }
    ]
};

describe('StackTraceFrame', () => {
    it('renders the complete formatted stack frame', () => {
        const { container } = render(StackTraceFrame, { frame });

        expect(container.textContent).toBe(
            'at Acme.WidgetNested.Run<T>(System.String\u00a0name,\u00a0System.Collections.Generic.List<Item>\u00a0items)\u00a0at offset 12\u00a0in /src/widget.cs:line 42:col 7'
        );
    });

    it('renders an anonymous frame without optional metadata', () => {
        const { container } = render(StackTraceFrame, { frame: {} });

        expect(container.textContent).toBe('at <anonymous>()');
    });

    it('does not create an anchor for every formatted field', () => {
        const { container } = render(StackTraceFrame, { frame });
        const walker = document.createTreeWalker(container, NodeFilter.SHOW_COMMENT);
        let commentCount = 0;

        while (walker.nextNode()) {
            commentCount++;
        }

        expect(commentCount).toBeLessThanOrEqual(2);
    });
});
