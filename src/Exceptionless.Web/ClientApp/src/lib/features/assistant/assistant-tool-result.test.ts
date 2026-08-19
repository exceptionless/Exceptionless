import { describe, expect, it } from 'vitest';

import { assistantToolErrorMessage, assistantToolResultFailed, formatAssistantToolJson } from './assistant-tool-result';

describe('assistantToolResultFailed', () => {
    it('recognizes a structured tool failure', () => {
        expect(assistantToolResultFailed('{"ok":false,"error":{"code":"unknown_filter_field"}}')).toBe(true);
    });

    it('does not mark a successful result as failed', () => {
        expect(assistantToolResultFailed('{"ok":true,"data":{"items":[]}}')).toBe(false);
    });

    it('treats an unknown result shape as successful transport', () => {
        expect(assistantToolResultFailed('not json')).toBe(false);
    });

    it('extracts structured error messages and formats raw JSON', () => {
        const result = '{"ok":false,"error":{"message":"Choose a project."}}';
        expect(assistantToolErrorMessage(result)).toBe('Choose a project.');
        expect(formatAssistantToolJson(result)).toContain('\n  "error"');
        expect(formatAssistantToolJson('plain text')).toBe('plain text');
    });
});
