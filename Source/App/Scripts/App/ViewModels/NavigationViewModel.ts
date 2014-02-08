/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class NavigationViewModel extends ViewModelBase {
        private _projectListViewModel: ProjectListViewModel;
        selectedProjectId = ko.observable<string>('');

        constructor (elementId: string, projectListViewModel?: ProjectListViewModel, projectId?: string) {
            super(elementId);

            this._projectListViewModel = projectListViewModel;
            if (this._projectListViewModel) {
                App.selectedProject.subscribe((project: models.ProjectInfo) => this.selectedProjectId(project.id));
            } else if (!StringUtil.isNullOrEmpty(projectId)) {
                this.selectedProjectId(projectId);
                App.projects.subscribe((projects: models.ProjectData[]) => {
                    var project = ko.utils.arrayFirst(projects, (project: models.Project) => project.id === projectId);
                    if (project)
                        App.selectedProject(<models.ProjectData>project); // TODO: Get rid of this cast.
                });
            } else {
                var id = DataUtil.getProjectId();
                if (!StringUtil.isNullOrEmpty(id))
                    this.selectedProjectId(id);
                else
                    console.log('Unable to resolve default projectId');
            }

            // TODO: Update new count.
            // TODO: Disable menu items if no selected project exists.

            this.applyBindings();
        }

        public applyBindings() {
            super.applyBindings();

            var element = document.getElementById("header-nav");
            if (element != null) {
                ko.applyBindings(this, element);
            }
        }
    }
}