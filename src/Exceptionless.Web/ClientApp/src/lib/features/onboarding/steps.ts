import type { OnboardingStep } from './models';

export const ONBOARDING_STEPS: readonly { description: string; id: OnboardingStep; label: string }[] = [
    { description: 'Tell me what your app is called.', id: 'name-project', label: 'Name your project' },
    { description: 'Paste a few lines into your app. I’ll show you exactly what.', id: 'install-client', label: 'Install the client' },
    { description: 'Send a test error and watch it arrive in real time.', id: 'catch-event', label: 'Catch your first event' }
];
