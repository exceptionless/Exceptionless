/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class ProjectListViewModel extends ViewModelBase {
        private _defaultProjectId: string;
        
        organizations: KnockoutComputed<models.Organization[]>;
        selectedProject: KnockoutComputed<{ id: string; organization: models.Organization; totalErrorCount: number; }>;

        constructor (elementId: string, defaultProjectId?: string) {
            super(elementId);

            this._defaultProjectId = defaultProjectId;
            if (!this._defaultProjectId) {
                App.selectedProject.subscribe((project: models.ProjectInfo) => localStorage.setItem(Constants.PROJECT, project.id));

                window.addEventListener('popstate', (ev: PopStateEvent) => { if (ev.state && !ev.state.internal) this.navigate(ev); });
            }

            App.selectedProject.subscribe((project: models.ProjectInfo) => DateUtil.timeZoneOffset(project.timeZoneOffset));

            this.organizations = ko.computed(() => App.organizations(), this);
            this.selectedProject = ko.computed(() => App.selectedProject(), this);
       
            App.projects.subscribe((projects: models.ProjectInfo[]) => {
                var project: models.ProjectInfo;
                var projectId: string = this._defaultProjectId ? this._defaultProjectId : DataUtil.getProjectId();
                if (!StringUtil.isNullOrEmpty(projectId)) {
                    project = ko.utils.arrayFirst(App.projects(), (p: models.ProjectInfo) => { return p.id === projectId; });
                }

                project = project ? project : App.projects()[0];
                if (!project || project.id === App.selectedProject().id)
                    return;

                App.selectedProject(project);

                if (this._defaultProjectId)
                    return;

                var state = $.extend(history.state ? history.state : {}, { project: { id: project.id, name: project.name, organizationId: project.organizationId, timeZoneOffset: project.timeZoneOffset, totalErrorCount: project.totalErrorCount } });
                history.replaceState(state, project.name, this.updateNavigationUrl());
            });

            this.applyBindings();
        }

        public applyBindings() {
            this.onProjectClick = <any>this.onProjectClick.bind(this);
            
            super.applyBindings();
        }

        public static getProjectId(): string {
            var pathName = location.pathname;
            // HACK: force IE9 to use isolated storage if the hash contains history state.
            if (location.hash.length > 0) {
                var hashes = location.hash.split('#');
                if (hashes.length > 0 && hashes[1].indexOf(Constants.PROJECT) > 0) {
                    pathName = '';
                }
            }

            var projectId: string;
            var paths = pathName.split('/');
            if (paths.length >= 3 && paths[1] === Constants.PROJECT && paths[2].length === 24)
                projectId = paths[2];
            else
                projectId = DataUtil.getValue(Constants.PROJECT);

            return projectId;
        }

        private navigate(ev: PopStateEvent) {
            if (ev.state && ev.state.project) {
                var project = new models.ProjectInfo(ev.state.project.id, ev.state.project.name, ev.state.project.organizationId, ev.state.project.timeZoneOffset, ev.state.project.stackCount, ev.state.project.errorCount, ev.state.project.totalErrorCount);
                if (project.id != App.selectedProject().id)
                    App.selectedProject(project);
            }
        }

        private updateNavigationUrl(): string {
            var projectId = App.selectedProject().id;
            var pathName = location.pathname;
            if (StringUtil.isNullOrEmpty(pathName) || pathName === '/') {
                pathName = StringUtil.format('/{controller}/{id}', { controller: Constants.PROJECT, id: projectId });
            } else {
                var paths = location.pathname.split('/');
                if (paths.length >= 2 && paths[1] === Constants.PROJECT) {
                    if (paths.length === 2 || (paths.length === 3 && paths[2] === '')) { // /project
                        pathName += '/' + projectId;
                    } else if (paths[2].length === 24) { // /project/{projectid}
                        pathName = pathName.replace(paths[2], projectId);
                    } else if (paths.length === 3) { // /project/{action}
                        switch (paths[2]) {
                            case Constants.PROJECT_RECENT:
                                pathName = pathName.replace(paths[2], StringUtil.format('{id}/{action}', { id: projectId, action: Constants.PROJECT_RECENT }));
                                break;
                            case Constants.PROJECT_FREQUENT:
                                pathName = pathName.replace(paths[2], StringUtil.format('{id}/{action}', { id: projectId, action: Constants.PROJECT_FREQUENT }));
                                break;
                            case Constants.PROJECT_NEW:
                                pathName = pathName.replace(paths[2], StringUtil.format('{id}/{action}', { id: projectId, action: Constants.PROJECT_NEW }));
                                break;
                        }
                    }
                }
            }
            return pathName + location.hash + location.search;
        }

        private onProjectClick(project: models.ProjectInfo): boolean {
            if (this._defaultProjectId)
                return true;

            App.selectedProject(project);

            var state = $.extend(history.state ? history.state : {}, { project: { id: project.id, name: project.name, organizationId: project.organizationId, timeZoneOffset: project.timeZoneOffset, totalErrorCount: project.totalErrorCount } });
            history.pushState(state, project.name, this.updateNavigationUrl());

            return false;
        }
    }
}