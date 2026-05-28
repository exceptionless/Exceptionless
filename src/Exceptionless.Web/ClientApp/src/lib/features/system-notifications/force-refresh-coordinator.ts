/**
 * Coordinates force-refresh behavior between the admin page and the global banner.
 *
 * When an admin triggers force-refresh, the server publishes a critical ReleaseNotification
 * that would normally cause window.location.reload() immediately — before the success toast
 * can render. This module lets the initiating tab delay its own reload by 1.5s while all
 * other connected clients still reload immediately.
 */
let _selfInitiated = false;

/**
 * Returns true and clears the flag if a self-initiated force-refresh is pending.
 * Returns false for all other clients.
 */
export function consumeSelfInitiatedFlag(): boolean {
    const was = _selfInitiated;
    _selfInitiated = false;
    return was;
}

/** Call before sending a force-refresh API request from the admin UI. */
export function flagSelfInitiatedForceRefresh(): void {
    _selfInitiated = true;
}
