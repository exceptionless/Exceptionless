import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import SavedViewNavigationGuard from './saved-view-navigation-guard.svelte';

const navigation = vi.hoisted(() => ({
    beforeNavigate: vi.fn(),
    goto: vi.fn()
}));
const pageState = vi.hoisted(() => ({
    url: 'http://localhost/next/event'
}));

vi.mock('$app/navigation', () => navigation);
vi.mock('$app/state', () => ({
    page: {
        get url() {
            return new URL(pageState.url);
        }
    }
}));

interface NavigationAttempt {
    cancel: () => void;
    to: null | { url: URL };
    willUnload: boolean;
}

function getBeforeNavigation(): (navigationAttempt: NavigationAttempt) => void {
    const callback = navigation.beforeNavigate.mock.calls[0]?.[0];
    if (!callback) {
        throw new Error('beforeNavigate was not registered');
    }

    return callback;
}

describe('SavedViewNavigationGuard', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        pageState.url = 'http://localhost/next/event';
        navigation.goto.mockResolvedValue(undefined);
    });

    it('allows navigation when the view is unchanged', () => {
        const cancel = vi.fn();
        render(SavedViewNavigationGuard, {
            isModified: false,
            onDiscard: vi.fn(),
            onSave: vi.fn(),
            saving: false
        });

        getBeforeNavigation()({
            cancel,
            to: { url: new URL('http://localhost/next/stack') },
            willUnload: false
        });

        expect(cancel).not.toHaveBeenCalled();
        expect(screen.queryByRole('alertdialog')).toBeNull();
    });

    it('allows query changes within the active saved view', () => {
        const cancel = vi.fn();
        pageState.url = 'http://localhost/next/stream?saved=first&filter=old';
        render(SavedViewNavigationGuard, {
            isModified: true,
            onDiscard: vi.fn(),
            onSave: vi.fn(),
            saving: false
        });

        getBeforeNavigation()({
            cancel,
            to: { url: new URL('http://localhost/next/stream?saved=first&filter=new') },
            willUnload: false
        });

        expect(cancel).not.toHaveBeenCalled();
        expect(screen.queryByRole('alertdialog')).toBeNull();
    });

    it('confirms before switching query-based saved views', async () => {
        const cancel = vi.fn();
        pageState.url = 'http://localhost/next/stream?saved=first';
        render(SavedViewNavigationGuard, {
            isModified: true,
            onDiscard: vi.fn(),
            onSave: vi.fn(),
            saving: false
        });

        getBeforeNavigation()({
            cancel,
            to: { url: new URL('http://localhost/next/stream?saved=second') },
            willUnload: false
        });

        expect(cancel).toHaveBeenCalledOnce();
        expect(await screen.findByRole('alertdialog')).not.toBeNull();
    });

    it('requests native confirmation before unloading a modified view', () => {
        const cancel = vi.fn();
        render(SavedViewNavigationGuard, {
            isModified: true,
            onDiscard: vi.fn(),
            onSave: vi.fn(),
            saving: false
        });

        getBeforeNavigation()({ cancel, to: null, willUnload: true });

        expect(cancel).toHaveBeenCalledOnce();
        expect(screen.queryByRole('alertdialog')).toBeNull();
    });

    it('stays on the page when the user cancels navigation', async () => {
        const cancel = vi.fn();
        render(SavedViewNavigationGuard, {
            isModified: true,
            onDiscard: vi.fn(),
            onSave: vi.fn(),
            saving: false
        });

        getBeforeNavigation()({
            cancel,
            to: { url: new URL('http://localhost/next/stack') },
            willUnload: false
        });

        expect(cancel).toHaveBeenCalledOnce();
        expect(await screen.findByRole('alertdialog')).not.toBeNull();

        await fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

        await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());
        expect(navigation.goto).not.toHaveBeenCalled();
    });

    it('discards changes before continuing to the requested page', async () => {
        const onDiscard = vi.fn();
        const destination = new URL('http://localhost/next/stack');
        render(SavedViewNavigationGuard, {
            isModified: true,
            onDiscard,
            onSave: vi.fn(),
            saving: false
        });

        getBeforeNavigation()({ cancel: vi.fn(), to: { url: destination }, willUnload: false });
        await fireEvent.click(await screen.findByRole('button', { name: "Don't save" }));

        expect(onDiscard).toHaveBeenCalledOnce();
        expect(navigation.goto).toHaveBeenCalledWith(destination);
    });

    it('continues only after the view saves successfully', async () => {
        const onSave = vi.fn().mockResolvedValueOnce(false).mockResolvedValueOnce(true);
        const destination = new URL('http://localhost/next/stack');
        render(SavedViewNavigationGuard, {
            isModified: true,
            onDiscard: vi.fn(),
            onSave,
            saving: false
        });

        getBeforeNavigation()({ cancel: vi.fn(), to: { url: destination }, willUnload: false });
        await fireEvent.click(await screen.findByRole('button', { name: 'Save' }));

        expect(onSave).toHaveBeenCalledOnce();
        expect(navigation.goto).not.toHaveBeenCalled();
        expect(screen.getByRole('alertdialog')).not.toBeNull();

        await fireEvent.click(screen.getByRole('button', { name: 'Save' }));

        await waitFor(() => expect(navigation.goto).toHaveBeenCalledWith(destination));
    });
});
