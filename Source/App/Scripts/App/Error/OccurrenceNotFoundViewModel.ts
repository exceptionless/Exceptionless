/// <reference path="../exceptionless.ts" />

module exceptionless.error {
    export class OccurrenceNotFoundViewModel extends ViewModelBase {
        private _navigationViewModel: NavigationViewModel;

        constructor(elementId: string, navigationElementId: string, defaultProjectId?: string, autoUpdate?: boolean, data?: JSON) {
            super(elementId, null, autoUpdate);

            this._navigationViewModel = new NavigationViewModel(navigationElementId, null, defaultProjectId);
            App.onPlanChanged.subscribe(() => window.location.reload());
            this.applyBindings();
        }
    }
}