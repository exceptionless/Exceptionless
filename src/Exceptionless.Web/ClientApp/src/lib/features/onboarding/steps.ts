import type { OnboardingStep } from './models';

export const ONBOARDING_STEPS: readonly { id: OnboardingStep; label: string }[] = [
    { id: 'name-project', label: 'Name your project' },
    { id: 'install-client', label: 'Install the client' },
    { id: 'catch-event', label: 'Catch your first event' }
];
