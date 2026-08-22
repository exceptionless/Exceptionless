import type { ErrorInfo } from '$features/events/models/event-data';

import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import SourceMapStatus from './source-map-status.svelte';

const projectId = '507f1f77bcf86cd799439011';

describe('SourceMapStatus', () => {
    it('renders source map failures with a management link', () => {
        const error: ErrorInfo = {
            data: {
                '@source_map': {
                    failures: [
                        {
                            generated_file_name: 'https://cdn.example.com/assets/app.min.js',
                            reason: 'invalid'
                        }
                    ],
                    status: 'failed'
                }
            }
        };

        render(SourceMapStatus, { error, projectId });

        expect(screen.getByRole('alert')).toBeTruthy();
        expect(screen.getByText('Source map unavailable')).toBeTruthy();
        expect(screen.getByText('https://cdn.example.com/assets/app.min.js')).toBeTruthy();
        expect(screen.getByText(/downloaded source map is invalid or unsupported/i)).toBeTruthy();
        expect(screen.getByRole('link', { name: 'Manage source maps' }).getAttribute('href')).toBe(`/next/project/${projectId}/source-maps`);
    });

    it('does not render without failure metadata', () => {
        render(SourceMapStatus, { error: {}, projectId });

        expect(screen.queryByRole('alert')).toBeNull();
    });

    it('renders when source map processing reaches the frame limit', () => {
        const error: ErrorInfo = {
            data: {
                '@source_map': {
                    failures: [],
                    processing_truncated: true,
                    status: 'failed'
                }
            }
        };

        render(SourceMapStatus, { error, projectId });

        expect(screen.getByRole('alert')).toBeTruthy();
        expect(screen.getByText(/stack-frame processing limit/i)).toBeTruthy();
    });
});
