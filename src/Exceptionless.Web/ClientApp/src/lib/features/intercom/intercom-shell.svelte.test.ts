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
        window.Intercom = vi.fn();
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

    it('updates after boot only when the route or boot options change', async () => {
        // Arrange
        const bootOptions = { intercomUserJwt: 'token_0', userId: 'user_123' } as BootOptions;
        const { rerender } = render(IntercomShellTestHarness, {
            props: {
                appId: 'app_123',
                bootOptions,
                routeKey: '/event/all'
            }
        });
        await tick();

        // Assert initial boot options are not immediately sent again.
        expect(intercomUpdate).not.toHaveBeenCalled();

        // Act
        await rerender({ appId: 'app_123', bootOptions, routeKey: '/stack/all' });

        // Assert
        expect(intercomUpdate).toHaveBeenCalledOnce();
        expect(intercomUpdate).toHaveBeenLastCalledWith({
            last_request_at: expect.any(Number),
            user_id: 'user_123'
        });

        // Act
        await rerender({
            appId: 'app_123',
            bootOptions: { intercomUserJwt: 'token_1', userId: 'user_123' } as BootOptions,
            routeKey: '/stack/all'
        });

        // Assert
        expect(intercomUpdate).toHaveBeenCalledTimes(2);
        expect(intercomUpdate).toHaveBeenLastCalledWith({
            intercom_user_jwt: 'token_1',
            user_id: 'user_123'
        });
    });

    it('does not update when navigation stays within the same normalized route', async () => {
        const bootOptions = { email: 'user@example.com', userId: 'user_123' } as BootOptions;
        const routeKey = '/(app)/project/[projectId]/event/[eventId]';
        const { rerender } = render(IntercomShellTestHarness, {
            props: { appId: 'app_123', bootOptions, routeKey }
        });
        await tick();

        await rerender({ appId: 'app_123', bootOptions, routeKey });

        expect(intercomUpdate).not.toHaveBeenCalled();
    });

    it('does not update before the client SDK initializes', async () => {
        // Arrange
        window.Intercom = undefined;
        const bootOptions = { intercomUserJwt: 'token_0', userId: 'user_123' } as BootOptions;
        const { rerender } = render(IntercomShellTestHarness, {
            props: {
                appId: 'app_123',
                bootOptions,
                routeKey: '/event/all'
            }
        });
        await tick();

        // Act
        await rerender({ appId: 'app_123', bootOptions, routeKey: '/stack/all' });

        // Assert
        expect(intercomUpdate).not.toHaveBeenCalled();

        // Act
        window.Intercom = vi.fn();
        await rerender({ appId: 'app_123', bootOptions, routeKey: '/event/errors' });

        // Assert
        expect(intercomUpdate).toHaveBeenCalledOnce();
    });
});
