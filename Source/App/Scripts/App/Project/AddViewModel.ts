/// <reference path="../exceptionless.ts" />

module exceptionless.project {
    export class AddViewModel extends ViewModelBase {
        private _organizationsDropDown: any;
        private _organizationTextBox: any;
        private _navigationViewModel: NavigationViewModel;
        
        hasExistingOrganization = ko.observable<boolean>(false);
        canCreateOrganization = ko.observable<boolean>(true);

        constructor(elementId: string, navigationElementId: string, existingOrganizationElementId: string, newOrganizationElementId: string, timeZoneElementId: string) {
            super(elementId);

            TimeZoneUtil.setDefaultTimeZone(timeZoneElementId);

            // TODO: We need to rewrite this entire page as its a cluster.....
            var hasOrganizations = $(existingOrganizationElementId + ' option').length > 0;
            this.hasExistingOrganization(hasOrganizations);
            this.canCreateOrganization(!hasOrganizations);

            this._organizationTextBox = $(newOrganizationElementId);
            this._organizationsDropDown = $(existingOrganizationElementId);

            this.freeOrganizations.subscribe((organizations: models.Organization[]) => {
                $(existingOrganizationElementId + ' option[value="__neworg__"]').remove();
                if (organizations === null || organizations.length === 0)
                    this._organizationsDropDown.append(new Option('<New Organization>', '__neworg__', false, false));
            });

            this._organizationsDropDown.change((event) => {
                this._organizationTextBox.val('');
                if (event.target.value === '__neworg__') {
                    this.canCreateOrganization(true);
                    this._organizationTextBox.focus();
                } else {
                    this.canCreateOrganization(false);
                }

                return true;
            });

            if (!StringUtil.isNullOrEmpty(navigationElementId))
                this._navigationViewModel = new NavigationViewModel(navigationElementId);

            this.applyBindings();
        }

        public addProject(): boolean {
            if (!StringUtil.isNullOrEmpty(this._organizationTextBox.val()) && this.freeOrganizations().length > 0) {
                bootbox.confirm('You already have one free account. You are not allowed to create more than one free account.', 'Cancel', 'Upgrade Plan', (result: boolean) => {
                    if (result)
                        App.showChangePlanDialog(this.freeOrganizations()[0]);
                });
                return false;
            } else if (this._organizationsDropDown.val() != null && this._organizationsDropDown.val() !== '__neworg__') {
                var org: models.Organization = ko.utils.arrayFirst(App.organizations(), (o: models.Organization) => o.id === this._organizationsDropDown.val());
                var projects = ko.utils.arrayFilter(App.projects(), (project: models.ProjectInfo) => project.organizationId === org.id);

                if (org.selectedPlan.maxProjects != -1 && projects.length >= org.selectedPlan.maxProjects) {
                    var message = 'You have exceeded your project limit of ' + org.selectedPlan.maxProjects + ' project';
                    if (org.selectedPlan.maxProjects > 1)
                        message += 's';
                    message += '. Upgrade your plan to add an additional project.';

                    bootbox.confirm(message, 'Cancel', 'Upgrade Plan', (result: boolean) => {
                        if (result)
                            App.showChangePlanDialog(org);
                    });

                    return false;
                }
            }

            return true;
        }

        public get freeOrganizations(): KnockoutComputed<models.Organization[]> {
            return ko.computed(() => ko.utils.arrayFilter(App.organizations(), (o: models.Organization) => o.selectedPlan.id === Constants.FREE_PLAN_ID), this);
        }

        public applyBindings() {
            super.applyBindings();
        }
    }
}