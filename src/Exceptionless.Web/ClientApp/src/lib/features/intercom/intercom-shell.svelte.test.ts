import type { BootOptions } from 'svelte-intercom';

import { fireEvent, render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import IntercomShellTestHarness from './intercom-shell.test-harness.svelte';

const intercomShowMessages = vi.hoisted(() => vi.fn());
const intercomUpdate = vi.hoisted(() => vi.fn());

vi.mock('$features/auth/index.svelte', () => ({
    accessToken: { current: 'token_123' }
}));

vi.mock('@intercom/messenger-js-sdk', () => ({
    boot: vi.fn(),
    getVisitorId: vi.fn(),
    hide: vi.fn(),
    Intercom: vi.fn(),
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
    update: intercomUpdate
}));

describe('IntercomShell', () => {
    beforeEach(() => {
        intercomShowMessages.mockReset();
        intercomUpdate.mockReset();
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

    it('remains stable across repeated tab visibility changes', async () => {
        // Arrange
        let hidden = false;
        const addEventListener = vi.spyOn(document, 'addEventListener');
        vi.spyOn(document, 'hidden', 'get').mockImplementation(() => hidden);
        const { rerender } = render(IntercomShellTestHarness, {
            props: {
                appId: 'app_123',
                bootOptions: { intercomUserJwt: 'token_0', userId: 'user_123' } as BootOptions
            }
        });
        await tick();

        // Act
        for (let index = 0; index < 100; index++) {
            hidden = true;
            document.dispatchEvent(new Event('visibilitychange'));
            await tick();

            await rerender({
                appId: 'app_123',
                bootOptions: { intercomUserJwt: `token_${index + 1}`, userId: 'user_123' } as BootOptions
            });

            hidden = false;
            document.dispatchEvent(new Event('visibilitychange'));
            await tick();
        }

        // Assert
        expect(intercomUpdate).toHaveBeenCalled();
        expect(addEventListener.mock.calls.filter(([eventName]) => eventName === 'visibilitychange')).toHaveLength(1);
    });
});
