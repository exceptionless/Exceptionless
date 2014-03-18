/// <reference path="../exceptionless.ts" />

module exceptionless.project {
    export class ConfigureViewModel extends ReportViewModelBase {
        projectTypes = ko.observableArray<models.KeyValuePair>([]);
        selectedProjectType = ko.observable<models.KeyValuePair>(new models.KeyValuePair('', ''));
        apiKey = ko.observable<string>('Loading');

        constructor(elementId: string, navigationElementId: string, chartElementId: string, projectsElementId: string, dateRangeElementId: string, copyApiKeyButtonElementId: string, autoUpdate?: boolean) {
            super(elementId, navigationElementId, chartElementId, '/project', projectsElementId, dateRangeElementId, null, autoUpdate);

            var clip = new ZeroClipboard();
            clip.on('noflash wrongflash', () => $(copyApiKeyButtonElementId).hide());
            clip.on('load', (client, text) => {
                clip.forceHandCursor(true);
                clip.clip($(copyApiKeyButtonElementId));
            });
            clip.on('complete', (client, text) => App.showSuccessNotification('Copied!'));

            App.selectedProject.subscribe((project: models.ProjectInfo) => {
                this.apiKey('Loading');
                var url = StringUtil.format('{url}/{id}/get-key', { url: this.baseUrl, id: project.id });
                this.retrieve(url, (data) => this.apiKey(data), () => {
                    this.apiKey('An error occurred');
                    App.showErrorNotification('An error occurred while retrieving the API key for your project.');
                });
            });

            exceptionless.App.onErrorOccurred.subscribe(() => {
                if (App.selectedProject().totalErrorCount === 0) {
                    window.location.href = '/project/' + App.selectedProject().id;
                }
            });

            this.populateViewModel();
            this.applyBindings();
        }

        public populateViewModel(data?: any) {
            this.projectTypes.push(new models.KeyValuePair('Exceptionless.Mvc', 'ASP.NET MVC'));
            this.projectTypes.push(new models.KeyValuePair('Exceptionless.WebApi', 'ASP.NET Web API'));
            this.projectTypes.push(new models.KeyValuePair('Exceptionless.Web', 'ASP.NET Web Forms'));
            this.projectTypes.push(new models.KeyValuePair('Exceptionless.Windows', 'Windows Forms'));
            this.projectTypes.push(new models.KeyValuePair('Exceptionless.Wpf', 'Windows Presentation Foundation (WPF)'));
            this.projectTypes.push(new models.KeyValuePair('Exceptionless.Nancy', 'Nancy'));
            this.projectTypes.push(new models.KeyValuePair('Exceptionless', 'Console'));
            this.selectedProjectType(this.projectTypes()[0]);
        }

        public updateSelectedProjectType(projectType: models.KeyValuePair) {
            this.selectedProjectType(projectType);
        }

        public applyBindings() {
            this.updateSelectedProjectType = <any>this.updateSelectedProjectType.bind(this);

            super.applyBindings();
        }

        public get applicationConfigName(): KnockoutComputed<string> {
            return ko.computed(() => {
                if (this.selectedProjectType().key() === 'Exceptionless.Mvc'
                    || this.selectedProjectType().key() === 'Exceptionless.WebApi'
                    || this.selectedProjectType().key() === 'Exceptionless.Web')
                    return 'web.config';

                return 'app.config';
            }, this);
        }
    }
}