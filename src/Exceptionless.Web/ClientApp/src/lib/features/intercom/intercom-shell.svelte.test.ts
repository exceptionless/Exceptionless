import { fireEvent, render, screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { BootOptions } from 'svelte-intercom';

import IntercomShellTestHarness from './intercom-shell.test-harness.svelte';

const intercomShowMessages = vi.hoisted(() => vi.fn());

vi.mock('$features/auth/index.svelte', () => ({
    accessToken: { current: 'token_123' }
}));

vi.mock('@intercom/messenger-js-sdk', () => ({
    Intercom: vi.fn(),
    boot: vi.fn(),
    getVisitorId: vi.fn(),
    hide: vi.fn(),
    onHide: vi.fn(),
    onShow: vi.fn(),
    onUnreadCountChange: vi.fn(),
    onUserEmailSupplied: vi.fn(),
    show: vi.fn(),
    showArticle: vi.fn(),
    showConversation: vi.fn(),
    showMessages: intercomShowMessages,
    showNewMessage: vi.fn(),
    showNews: vi.fn(),
    showSpace: vi.fn(),
    showTicket: vi.fn(),
    shutdown: vi.fn(),
    startChecklist: vi.fn(),
    startSurvey: vi.fn(),
    startTour: vi.fn(),
    trackEvent: vi.fn(),
    update: vi.fn()
}));

describe('IntercomShell', () => {
    beforeEach(() => {
        intercomShowMessages.mockReset();
        vi.restoreAllMocks();
    });

    it('keeps children mounted when Intercom becomes bootable', async () => {
        // Arrange
        let mountCount = 0;
        const openWindow = vi.spyOn(window, 'open').mockImplementation(() => null);
        const { rerender } = render(IntercomShellTestHarness, {
            props: {
                appId: 'app_123',
                bootOptions: undefined,
                onMountProbe: () => {
                    mountCount += 1;
                }
            }
        });

        // Act
        await fireEvent.click(screen.getByTestId('open-chat'));
        await rerender({
            appId: 'app_123',
            bootOptions: { userId: 'user_123' } as BootOptions,
            onMountProbe: () => {
                mountCount += 1;
            }
        });
        await fireEvent.click(screen.getByTestId('open-chat'));

        // Assert
        expect(mountCount).toBe(1);
        expect(openWindow).toHaveBeenCalledTimes(1);
        expect(intercomShowMessages).toHaveBeenCalledTimes(1);
    });
});