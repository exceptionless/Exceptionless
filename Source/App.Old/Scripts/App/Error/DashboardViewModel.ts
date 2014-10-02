/// <reference path="../exceptionless.ts" />

module exceptionless.error {
    export class DashboardViewModel extends ViewModelBase {
        private _navigationViewModel: NavigationViewModel;

        projectId = ko.observable<string>('');
        currentTabHash = ko.observable<string>('');
        installDate = ko.observable<Date>(DateUtil.minValue.toDate());
        occurrence = ko.observable<Date>(DateUtil.minValue.toDate());
        previousErrorId = ko.observable<string>('');
        nextErrorId = ko.observable<string>('');
        previousErrorLink = ko.observable<string>('');
        nextErrorLink = ko.observable<string>('');
        promotedTabs = ko.observable<string[]>([]);

        constructor(elementId: string, navigationElementId: string, projectsElementId: string, dateRangeElementId: string, tabElementId: string, defaultProjectId?: string, autoUpdate?: boolean, data?: JSON) {
            super(elementId, null, autoUpdate);
            this.currentTabHash(location.hash);

            TabUtil.init(tabElementId);
            this._navigationViewModel = new NavigationViewModel(navigationElementId, null, defaultProjectId);
            App.onPlanChanged.subscribe(() => window.location.reload());

            window.addEventListener('hashchange', (hashChangeEvent: any) => {
                this.currentTabHash(location.hash);
            });

            $.each($('[data-bind-template]'), (key, value) => {
                var content = $(value).text();

                try {
                    var json = JSON.parse(content);
                    var template = HandlebarsUtil.getTemplate(value);
                    $(value).html(template(json));
                } catch (ex) {
                    $(value).text(content);
                }
            });

            this.populateViewModel(data);
            this.applyBindings();
            App.initZeroClipboard();
        }

        public populateViewModel(data?: any) {
            if (!data)
                return;

            this.projectId(data.ProjectId);
            this.occurrence(DateUtil.parse(data.OccurrenceDate));
            this.previousErrorId(data.PreviousErrorId);
            this.previousErrorLink(this.previousErrorId() == null || this.previousErrorId().length == 0 ? 'javascript:return false;' : '/error/' + this.previousErrorId());
            this.nextErrorId(data.NextErrorId);
            this.nextErrorLink(this.nextErrorId() == null || this.nextErrorId().length == 0 ? 'javascript:return false;' : '/error/' + this.nextErrorId());

            if (data.ExceptionlessClientInfo)
                this.installDate(DateUtil.parse(data.ExceptionlessClientInfo.InstallDate));

            if (data.PromotedTabs) {
                var tabs: string[] = [];
                for (var i = 0; i < data.PromotedTabs.length; i++) {
                    tabs.push(data.PromotedTabs[i]);
                }

                this.promotedTabs(tabs);
            }
        }

        public promoteTab(key: string) {
            // TODO: We need to add support for array item level patching.
            var url = StringUtil.format('/api/v1/project/{id}', { id: this.projectId() });

            var data = [key];
            ko.utils.arrayForEach(this.promotedTabs(), (item: string) => data.push(item));

            this.patch(url, { PromotedTabs: data },
                (data) => {
                    this.promotedTabs(data);
                    window.location.hash = '#ex-' + key.toLowerCase();
                    window.location.reload();
                },
                'An error occurred while promoting this tab.');
        }

        public demoteTab(key: string) {
            // TODO: We need to add support for array item level patching.
            var url = StringUtil.format('/api/v1/project/{id}', { id: this.projectId() });

            var data = [];
            ko.utils.arrayForEach(this.promotedTabs(), (item: string) => {
                if (item !== key)
                    data.push(item)
            });

            this.patch(url, { PromotedTabs: data },
                (data) => {
                    this.promotedTabs(data);
                    window.location.hash = '#extended';
                    window.location.reload();
                },
                'An error occurred while demoting this tab.');
        }
    }
}