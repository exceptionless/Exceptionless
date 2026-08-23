export function shouldResetActiveEventTab(eventLoaded: boolean, projectPending: boolean, tabs: readonly string[], activeTab: string): boolean {
    return eventLoaded && !projectPending && !tabs.includes(activeTab);
}
