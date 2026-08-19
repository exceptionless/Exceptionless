import type { ProductTourListItem } from '$features/product-tours/types';

import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { NavigationItem } from '../../routes.svelte';

const generateSampleDataMutateAsync = vi.hoisted(() => vi.fn());
const goto = vi.hoisted(() => vi.fn());
const inviteUserMutateAsync = vi.hoisted(() => vi.fn());
const logout = vi.hoisted(() => vi.fn());
const organizationState = vi.hoisted(() => ({ current: 'organization-id' as string | undefined }));
const refetchQueries = vi.hoisted(() => vi.fn());
const remoteSearchQueryState = vi.hoisted(() => ({ isPending: false }));
const resetDataMutateAsync = vi.hoisted(() => vi.fn());
const toast = vi.hoisted(() => ({ dismiss: vi.fn(), error: vi.fn(), success: vi.fn() }));
const toggleMode = vi.hoisted(() => vi.fn());
const project = vi.hoisted(() => ({
    id: '0123456789abcdef01234567',
    name: 'Test Project',
    organization_id: 'organization-id'
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$features/auth/api.svelte', () => ({ logout }));
vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'access-token' } }));
vi.mock('$features/events/components/summary/index', () => ({ buildEventDetailsHref: vi.fn() }));
vi.mock('$features/billing', () => ({ showBillingDialogOnUpgradeProblem: vi.fn(() => false) }));
vi.mock('$features/organizations/api.svelte', () => ({
    addOrganizationUser: () => ({ isPending: false, mutateAsync: inviteUserMutateAsync })
}));
vi.mock('$features/organizations/context.svelte', () => ({ organization: organizationState }));
vi.mock('$features/projects/api.svelte', () => ({
    generateSampleData: () => ({ isPending: false, mutateAsync: generateSampleDataMutateAsync }),
    getOrganizationProjectsQuery: () => ({ data: { data: [project] }, isError: false, isLoading: false }),
    resetData: () => ({ isPending: false, mutateAsync: resetDataMutateAsync })
}));
vi.mock('@foundatiofx/fetchclient', () => ({
    ProblemDetails: class ProblemDetails extends Error {},
    useFetchClient: () => ({ getJSON: vi.fn() })
}));
vi.mock('@tanstack/svelte-query', () => ({
    createQuery: () => ({
        data: undefined,
        get isPending() {
            return remoteSearchQueryState.isPending;
        }
    }),
    useQueryClient: () => ({ refetchQueries })
}));
vi.mock('mode-watcher', () => ({ toggleMode }));
vi.mock('svelte-sonner', () => ({ toast }));

import NavigationCommand from './navigation-command.svelte';

type RenderOptions = {
    askExie?: (prompt: string) => Promise<void> | void;
    guidedTours?: ProductTourListItem[];
    isChatEnabled?: boolean;
    isExieEnabled?: boolean;
    isGlobalAdmin?: boolean;
    isImpersonating?: boolean;
    openChat?: () => void;
    openExie?: () => Promise<void> | void;
    openGuidedTours?: () => void;
    openImpersonateOrganization?: () => Promise<void> | void;
    organizations?: Array<{ id: string; name: string }>;
    startGuidedTour?: (id: ProductTourListItem['id']) => void;
    stopImpersonating?: () => Promise<void> | void;
};

const sessionsRoute: NavigationItem = {
    group: 'Dashboards',
    href: '/next/sessions',
    icon: undefined as never,
    title: 'Sessions'
};

function renderCommandPalette(routes: NavigationItem[] = [], options: RenderOptions = {}) {
    return render(NavigationCommand, {
        askExie: options.askExie ?? vi.fn(),
        guidedTours: options.guidedTours ?? [],
        isChatEnabled: options.isChatEnabled ?? false,
        isExieEnabled: options.isExieEnabled ?? true,
        isGlobalAdmin: options.isGlobalAdmin ?? false,
        isImpersonating: options.isImpersonating ?? false,
        open: true,
        openChat: options.openChat ?? vi.fn(),
        openExie: options.openExie ?? vi.fn(),
        openGuidedTours: options.openGuidedTours ?? vi.fn(),
        openImpersonateOrganization: options.openImpersonateOrganization ?? vi.fn(),
        openKeyboardShortcuts: vi.fn(),
        openOrganizationSwitcher: vi.fn(),
        openUserMenu: vi.fn(),
        organizations: (options.organizations ?? []) as never,
        resetKey: 0,
        routes: [sessionsRoute, ...routes],
        startGuidedTour: options.startGuidedTour ?? vi.fn(),
        stopImpersonating: options.stopImpersonating ?? vi.fn()
    });
}

describe('NavigationCommand project actions', () => {
    beforeAll(() => {
        Element.prototype.scrollIntoView = vi.fn();
    });

    beforeEach(() => {
        generateSampleDataMutateAsync.mockResolvedValue(undefined);
        goto.mockResolvedValue(undefined);
        inviteUserMutateAsync.mockResolvedValue(undefined);
        logout.mockResolvedValue(undefined);
        organizationState.current = 'organization-id';
        refetchQueries.mockResolvedValue(undefined);
        remoteSearchQueryState.isPending = false;
        resetDataMutateAsync.mockResolvedValue(undefined);
    });

    it.each([
        ['Open Project', `/next/project/${project.id}/manage`],
        ['Project Stacks', `/next/stack?filter=project:${project.id}`],
        ['Project Events', `/next/event?project=${project.id}`],
        ['Project API Keys', `/next/project/${project.id}/api-keys`],
        ['Project Webhooks & Integrations', `/next/project/${project.id}/integrations`],
        ['Project Source Maps', `/next/project/${project.id}/source-maps`],
        ['Project Notifications', `/next/account/notifications?project=${project.id}`],
        ['Client Setup', `/next/project/${project.id}/configure`]
    ])('links %s to the selected project', async (action, expectedHref) => {
        renderCommandPalette();

        await fireEvent.click(screen.getByText(action));

        const projectLink = screen.getByText(project.name).closest('a');
        expect(projectLink?.getAttribute('href')).toBe(expectedHref);
        expect(screen.getByPlaceholderText('Select a project...')).toBeTruthy();
    });

    it('finds AI Tools by its MCP keyword', async () => {
        renderCommandPalette([
            {
                group: 'My Account',
                href: '/next/account/ai-tools',
                icon: undefined as never,
                keywords: ['MCP'],
                title: 'AI Tools'
            }
        ]);

        await fireEvent.input(screen.getByPlaceholderText('Search or jump to...'), { target: { value: 'MCP' } });

        const aiToolsGroup = screen.getByText('AI Tools').closest('[data-command-group]');
        await waitFor(() => expect(aiToolsGroup?.hasAttribute('hidden')).toBe(false));
    });

    it('finds project integrations by the webhook keyword', async () => {
        renderCommandPalette();

        await fireEvent.input(screen.getByPlaceholderText('Search or jump to...'), { target: { value: 'webhook' } });

        const integrationsCommand = screen.getByText('Project Webhooks & Integrations').closest('[data-command-item]');
        await waitFor(() => expect(integrationsCommand?.hasAttribute('data-selected')).toBe(true));
    });

    it('does not mount remote result groups while a search is pending', async () => {
        remoteSearchQueryState.isPending = true;
        renderCommandPalette();

        await fireEvent.input(screen.getByPlaceholderText('Search or jump to...'), { target: { value: 'no-local-match' } });

        await waitFor(() => expect(screen.getByText('Searching...')).toBeTruthy());
        expect(screen.queryByText('Searching events...')).toBeNull();
        expect(screen.queryByText('Searching stacks...')).toBeNull();
    });

    it('generates sample data for the selected project', async () => {
        renderCommandPalette();

        await fireEvent.click(screen.getByText('Generate Sample Data'));
        await fireEvent.click(screen.getByText(project.name));

        await waitFor(() => expect(generateSampleDataMutateAsync).toHaveBeenCalledOnce());
        expect(toast.success).toHaveBeenCalledWith(`Sample data generation has been queued for "${project.name}". Events will appear shortly.`);
    });

    it('opens the project picker when a filtered project action is selected with Enter', async () => {
        renderCommandPalette();

        const searchInput = screen.getByPlaceholderText('Search or jump to...');
        await fireEvent.input(searchInput, { target: { value: 'generate' } });
        await waitFor(() => expect(screen.getByText('Generate Sample Data').closest('[data-command-item]')?.hasAttribute('data-selected')).toBe(true));
        await fireEvent.keyDown(searchInput, { key: 'Enter' });

        expect(screen.getByPlaceholderText('Select a project...')).toBeTruthy();
        await waitFor(() => expect(screen.getByText(project.name)).toBeTruthy());
    });

    it('confirms before resetting the selected project data', async () => {
        renderCommandPalette();

        await fireEvent.click(screen.getByText('Reset Project Data'));
        await fireEvent.click(screen.getByText(project.name));
        expect((await screen.findByText(/Are you sure you want to reset all project data/)).textContent).toContain(project.name);

        await fireEvent.click(screen.getByRole('button', { name: 'Reset Project Data' }));

        await waitFor(() => expect(resetDataMutateAsync).toHaveBeenCalledOnce());
        expect(toast.success).toHaveBeenCalledWith(`Successfully queued "${project.name}" for data reset.`);
    });

    it('offers the approved app actions', () => {
        renderCommandPalette([], { isChatEnabled: true, isGlobalAdmin: true });

        for (const action of [
            'Add Organization',
            'View Organization Users',
            'Invite User',
            'Ask Exie',
            'Triage Recent Errors',
            'Analyze Error Trends',
            'Chat with Support',
            'Toggle Theme',
            'Refresh Current View',
            'Impersonate Organization',
            'Log Out'
        ]) {
            expect(screen.getByText(action)).toBeTruthy();
        }
    });

    it('links to the current organization users list', () => {
        renderCommandPalette();

        const usersLink = screen.getByText('View Organization Users').closest('a');

        expect(usersLink?.getAttribute('href')).toBe('/next/organization/organization-id/users');
    });

    it('opens Exie from the command palette', async () => {
        const openExie = vi.fn();
        renderCommandPalette([], { openExie });

        await fireEvent.click(screen.getByText('Ask Exie'));

        expect(openExie).toHaveBeenCalledOnce();
    });

    it.each([
        ['Triage Recent Errors', 'Triage the most important recent errors'],
        ['Analyze Error Trends', 'Analyze error trends in the current context over the last 7 days']
    ])('sends the %s prompt to Exie', async (command, expectedPrompt) => {
        const askExie = vi.fn();
        renderCommandPalette([], { askExie });

        await fireEvent.click(screen.getByText(command));

        expect(askExie).toHaveBeenCalledWith(expect.stringContaining(expectedPrompt));
    });

    it('hides Exie commands when Exie is disabled', () => {
        renderCommandPalette([], { isExieEnabled: false });

        expect(screen.queryByText('Ask Exie')).toBeNull();
        expect(screen.queryByText('Triage Recent Errors')).toBeNull();
        expect(screen.queryByText('Analyze Error Trends')).toBeNull();
    });

    it('invites a user to the current organization', async () => {
        renderCommandPalette();

        await fireEvent.click(screen.getByText('Invite User'));
        const emailInput = await screen.findByLabelText('Email Address');
        await fireEvent.input(emailInput, { target: { value: 'new.user@example.com' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Invite User' }));

        await waitFor(() => expect(inviteUserMutateAsync).toHaveBeenCalledWith('new.user@example.com'));
        expect(toast.success).toHaveBeenCalledWith('User invited successfully');
    });

    it('switches directly to a named organization', async () => {
        renderCommandPalette([], { organizations: [{ id: 'other-organization-id', name: 'Other Organization' }] });

        await fireEvent.click(screen.getByText('Switch to Other Organization'));

        expect(organizationState.current).toBe('other-organization-id');
        await waitFor(() => expect(goto).toHaveBeenCalledWith('/next/stack'));
    });

    it('opens support chat', async () => {
        const openChat = vi.fn();
        renderCommandPalette([], { isChatEnabled: true, openChat });
        await fireEvent.click(screen.getByText('Chat with Support'));
        expect(openChat).toHaveBeenCalledOnce();
    });

    it('toggles the theme', async () => {
        renderCommandPalette();
        await fireEvent.click(screen.getByText('Toggle Theme'));
        expect(toggleMode).toHaveBeenCalledOnce();
    });

    it('refreshes active data for the current view', async () => {
        renderCommandPalette();
        await fireEvent.click(screen.getByText('Refresh Current View'));
        await waitFor(() => expect(refetchQueries).toHaveBeenCalledWith({ type: 'active' }));
    });

    it('opens organization impersonation for global admins', async () => {
        const openImpersonateOrganization = vi.fn();
        renderCommandPalette([], { isGlobalAdmin: true, openImpersonateOrganization });
        await fireEvent.click(screen.getByText('Impersonate Organization'));
        expect(openImpersonateOrganization).toHaveBeenCalledOnce();
    });

    it('stops organization impersonation for global admins', async () => {
        const stopImpersonating = vi.fn();
        renderCommandPalette([], { isGlobalAdmin: true, isImpersonating: true, stopImpersonating });

        await fireEvent.click(screen.getByText('Stop Impersonating'));

        expect(stopImpersonating).toHaveBeenCalledOnce();
    });

    it('logs out the current user', async () => {
        renderCommandPalette();
        await fireEvent.click(screen.getByText('Log Out'));
        await waitFor(() => expect(logout).toHaveBeenCalledOnce());
        expect(goto).toHaveBeenCalledWith('/next/login');
    });
});

describe('NavigationCommand guided tours', () => {
    it('launches available tours directly and opens the full catalog', async () => {
        const openGuidedTours = vi.fn();
        const startGuidedTour = vi.fn();
        renderCommandPalette([], {
            guidedTours: [
                {
                    availability: { available: true },
                    description: 'Learn the UI.',
                    getAvailability: vi.fn(),
                    getSteps: vi.fn(),
                    id: 'new-ui-overview',
                    keywords: ['tour'],
                    title: 'Explore the new UI',
                    version: 1
                }
            ],
            openGuidedTours,
            startGuidedTour
        });

        await fireEvent.click(screen.getByText('Explore the new UI'));
        expect(startGuidedTour).toHaveBeenCalledWith('new-ui-overview');

        renderCommandPalette([], { openGuidedTours });
        await fireEvent.click(screen.getAllByText('Browse Guided Tours…').at(-1)!);
        expect(openGuidedTours).toHaveBeenCalled();
    });
});
