import type { Platform } from './platforms';

/** How long to wait for a first event before offering help. */
export const TROUBLESHOOTING_DELAY_SECONDS = 60;

export function formatElapsed(totalSeconds: number): string {
    const seconds = Math.max(0, Math.floor(totalSeconds));
    const minutes = Math.floor(seconds / 60);
    return `${minutes}:${String(seconds % 60).padStart(2, '0')}`;
}

/** Checks for a missing first event, ordered from most to least likely for the platform. */
export function getTroubleshootingTips(platform: Platform | undefined, serverUrl: string): string[] {
    const host = getHost(serverUrl);
    const tips = ['Copy the API key again and check the one in your app matches it exactly.'];

    if (platform?.isRawHttp) {
        tips.push('Check the command’s response: 202 Accepted means the event was queued; 401 means the API key was not accepted.');
    } else {
        tips.push('Restart your app after adding the code, then trigger the test error again.');
    }

    tips.push(`Make sure ${host} is reachable from where your app runs.`);

    if (platform?.isBrowser) {
        tips.push('Ad and privacy blockers can stop browser apps from sending events. Try a private window with extensions turned off.');
    }

    tips.push(`Firewalls and proxies need to allow outbound HTTPS to ${host}.`);

    if (platform && !platform.isRawHttp) {
        tips.push(`Turn on the client’s debug logging: ${platform.debugLogging}`);
    }

    return tips;
}

function getHost(serverUrl: string): string {
    try {
        return new URL(serverUrl).host;
    } catch {
        return 'your Exceptionless server';
    }
}
