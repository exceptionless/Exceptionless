export const PRODUCT_TOUR_ANCHORS = {
    appNavigation: 'app-navigation',
    commandSearch: 'command-search',
    eventDetails: 'event-details',
    eventList: 'event-list',
    exiePanel: 'exie-panel',
    exieTrigger: 'exie-trigger',
    helpMenu: 'help-menu',
    projectConfigureInstructions: 'project-configure-instructions',
    projectConfigurePlatform: 'project-configure-platform',
    projectConfigureToken: 'project-configure-token',
    projectConfigureWaiting: 'project-configure-waiting',
    projectName: 'project-name',
    projectSetupSubmit: 'project-setup-submit',
    savedViewDialog: 'saved-view-dialog',
    savedViewName: 'saved-view-name',
    savedViewNavigation: 'saved-view-navigation',
    savedViewPrivate: 'saved-view-private',
    savedViewSaveAs: 'saved-view-save-as',
    savedViewSubmit: 'saved-view-submit',
    savedViewTrigger: 'saved-view-trigger',
    setupOrganizationName: 'setup-organization-name'
} as const;

export function productTourSelector(anchor: string): string {
    return `[data-tour="${anchor}"]`;
}
