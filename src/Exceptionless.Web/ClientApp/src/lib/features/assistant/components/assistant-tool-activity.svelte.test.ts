import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import AssistantToolActivity from './assistant-tool-activity.svelte';

describe('AssistantToolActivity', () => {
    it('labels completed stack event requests', () => {
        render(AssistantToolActivity, {
            props: {
                tool: {
                    arguments: '{}',
                    id: 'tool-1',
                    name: 'get_stack_events',
                    result: '{"ok":true,"data":{"items":[]}}',
                    status: 'complete'
                }
            }
        });

        expect(screen.getByRole('button', { name: /Listed stack events/ })).toBeTruthy();
    });

    it('shows a concise failure and keeps raw request and response available', async () => {
        render(AssistantToolActivity, {
            props: {
                tool: {
                    arguments: '{"filter":"id:bad"}',
                    id: 'tool-1',
                    name: 'search_stacks',
                    result: '{"ok":false,"error":{"message":"Unknown filter field id."}}',
                    status: 'failed'
                }
            }
        });

        expect(screen.getByText('Unknown filter field id.')).toBeTruthy();
        await fireEvent.click(screen.getByRole('button', { name: /Couldn’t search error stacks/ }));

        expect(screen.getByRole('region', { name: 'Tool request' }).textContent).toContain('"filter": "id:bad"');
        expect(screen.getByRole('region', { name: 'Tool response' }).textContent).toContain('"ok": false');
    });
});
