/// <reference path="../exceptionless.ts" />

module exceptionless.project {
    export class ListViewModel extends PagedProjectInfosViewModel {
        private _navigationViewModel: NavigationViewModel;

        constructor(elementId: string, navigationElementId: string) {
            super(elementId, '/project/', 'list');
        
            this._navigationViewModel = new NavigationViewModel(navigationElementId);
        }
    }
}