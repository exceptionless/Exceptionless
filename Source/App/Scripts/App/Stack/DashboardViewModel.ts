/// <reference path="../exceptionless.ts" />

module exceptionless.stack {
    export class DashboardViewModel extends ReportViewModelBase {
        private _errorStackId: string;
        private _stats: KnockoutObservableArray<any> = ko.observableArray([]);
        private _pagedErrorsByErrorStackIdViewModel: error.PagedErrorsByErrorStackIdViewModel;
        private _chartOptions: any;
        
        total = ko.observable<number>(0);
        firstOccurrence = ko.observable<Date>(DateUtil.minValue.toDate());
        lastOccurrence = ko.observable<Date>(DateUtil.minValue.toDate());
        fixedOn = ko.observable<Date>(DateUtil.minValue.toDate());
        isFixed = ko.observable<boolean>(false);
        isRegressed = ko.observable<boolean>(false);
        isHidden = ko.observable<boolean>(false);
        disableNotifications = ko.observable<boolean>(false);
        occurrencesAreCritical = ko.observable<boolean>(false);
        
        references = ko.observableArray<string>([]);
        url = ko.observable<string>('').extend({ required: true, url: true });
        saveReferencesCommand: KoliteCommand;

        constructor(elementId: string, navigationElementId: string, projectsElementId: string, dateRangeElementId: string, chartElementId: string, recentElementId: string, dateFixedElementId: string, errorStackId: string, defaultProjectId?: string, pageSize?: number, autoUpdate?: boolean, data?: any) {
            super(elementId, navigationElementId, chartElementId, '/stats/stack/' + errorStackId, projectsElementId, dateRangeElementId, false, defaultProjectId, autoUpdate);

            this._errorStackId = errorStackId;

            this._stats.subscribe(() => this.tryUpdateChart());
            this.fixedOn.subscribe((date: Date) => this.isFixed(date != null));

            var notification = DataUtil.getQueryStringValue('notification');
            if (!StringUtil.isNullOrEmpty(notification)) {
                if (notification === 'mark-fixed') {
                    App.showSuccessNotification('Successfully marked the error stack as fixed.', null, { fadeOut: 10000, extendedTimeOut: 1000 });
                } else if (notification === 'stop-notifications') {
                    App.showSuccessNotification('Successfully updated the error stack notification settings.', null, { fadeOut: 10000, extendedTimeOut: 1000 });
                }

                history.replaceState(history.state, null, window.location.pathname);
            }

            this.saveReferencesCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    if (isExecuting)
                        return false;

                    return true;
                },
                execute: (complete) => {
                    if (!this.url.isValid()) {
                        this.errors.showAllMessages();
                        complete();
                        return;
                    }

                    var data: string[] = this.references();
                    data.push(this.url());

                    var url = StringUtil.format('/api/v1/stack/{id}', { id: this._errorStackId });
                    this.patch(url, { References: data },
                        (data) => {
                            $('#add-new-reference-modal').modal('hide');

                            this.references.push(this.url());
                            App.showSuccessNotification('Successfully added the external reference link.');
                            complete();
                        },
                        (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                            App.showErrorNotification('An error occurred while adding the external reference link.');
                            complete();
                        });
                }
            });

            this.errors = ko.validation.group(this, { deep: true });
            this.populateViewModel(data);
            this.applyBindings();

            exceptionless.App.onStackUpdated.subscribe((stack) => {
                if (stack.id === this._errorStackId && this.canRetrieve)
                    this.refreshViewModelData();
            });

            this.filterViewModel.selectedDateRange.subscribe(() => this.refreshViewModelData());

            this.refreshViewModelData();
            this._pagedErrorsByErrorStackIdViewModel = new error.PagedErrorsByErrorStackIdViewModel(recentElementId, errorStackId, this.projectListViewModel, this.filterViewModel, pageSize, autoUpdate);
        }

        public onStackUpdated(stack) {
            if (stack.id !== this._errorStackId)
                return;

            if (this.canRetrieve)
                this.refreshViewModelData();
        }

        public onNewError(error) {
            if (error.stackId !== this._errorStackId)
                return;

            if (this.canRetrieve)
                this.refreshViewModelData();
        }

        public populateViewModel(data?: any) {
            if (!data)
                return;

            if (data.hasOwnProperty('FirstOccurrence'))
                this.firstOccurrence(DateUtil.parse(data.FirstOccurrence));

            if (data.hasOwnProperty('LastOccurrence'))
                this.lastOccurrence(DateUtil.parse(data.LastOccurrence));

            if (data.hasOwnProperty('DisableNotifications'))
                this.disableNotifications(data.DisableNotifications);

            if (data.hasOwnProperty('IsHidden'))
                this.isHidden(data.IsHidden);

            if (data.hasOwnProperty('IsRegressed'))
                this.isRegressed(data.IsRegressed);

            if (data.hasOwnProperty('OccurrencesAreCritical'))
                this.occurrencesAreCritical(data.OccurrencesAreCritical);

            if (data.hasOwnProperty('Stats'))
                this._stats(data.Stats);

            if (data.hasOwnProperty('DateFixed'))
                this.fixedOn(data.DateFixed);

            if (data.hasOwnProperty('TotalOccurrences'))
                this.total(data.TotalOccurrences);

            else if (data.hasOwnProperty('Total'))
                this.total(data.Total);
            
            if (data.hasOwnProperty('References'))
                this.references(data.References);
        }

        public updateChart() {
            var chartData = [['Day', 'Occurrences']];
            this.chartOptions.series[0].data = [];

            var stats = this._stats();
            for (var x = 0; x < stats.length; x++) {
                this.chartOptions.series[0].data.push({ x: moment.utc(stats[x].Date).unix(), y: stats[x].Total, data: stats[x] });
            }

            this.chart.update();
            //this.chartSpinner.stop();
        }

        public get chartOptions(): any {
            if (!this._chartOptions) {
                this._chartOptions = {
                    element: document.querySelector(this.chartElementId),
                    renderer: 'stack',
                    stroke: true,
                    padding: { top: 0.085 },
                    series: [{
                        name: 'Occurrences',
                        color: 'rgba(115, 192, 58, 0.5)',
                        stroke: 'rgba(0,0,0,0.15)',
                        data: []
                    }]
                };
            }

            return this._chartOptions;
        }

        public createChartHoverDetail(graph: any): any {
            var Hover = Rickshaw.Class.create(Rickshaw.Graph.HoverDetail, {
                render: function (args) {
                    var date = moment.unix(args.domainX).utc();
                    var formattedDate = date.hours() === 0 ? DateUtil.formatWithMonthDayYear(date) : DateUtil.format(date);
                    var content = '<div class="date">' + formattedDate + '</div>';

                    var d = args.detail[0];
                    var swatch = '<span class="detail-swatch" style="background-color: ' + d.series.color.replace('0.5', '1') + '"></span>';
                    content += swatch + numeral(d.value.data.Total).format('0,0[.]0') + ' ' + d.series.name + ' <br />';

                    var xLabel = document.createElement('div');
                    xLabel.className = 'x_label';
                    xLabel.innerHTML = content;
                    this.element.appendChild(xLabel);

                    this.show();
                }
            });

            return new Hover({ graph: graph });
        }

        public promoteToExternal() {
            if (!App.selectedPlan().hasPremiumFeatures) {
                bootbox.confirm('Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.', 'Cancel', 'Upgrade Plan', (result: boolean) => {
                    if (result)
                        App.showChangePlanDialog();
                });
                return;
            }

            var url = StringUtil.format('/api/v1/stack/{id}/promote', { id: this._errorStackId });
            this.insert(null, url,
                () => App.showSuccessNotification('Successfully promoted error stack!'),
                (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    if (jqXHR.status === 426) {
                        var message = jqXHR.responseText;
                        try {
                            message = JSON.parse(jqXHR.responseText).Message;
                        } catch (e) {}

                        bootbox.confirm(message, 'Cancel', 'Upgrade Plan', (result: boolean) => {
                            if (result)
                                App.showChangePlanDialog();
                        });

                        return;
                    } else if (jqXHR.status === 501) {
                        var message = jqXHR.responseText;
                        try {
                            message = JSON.parse(jqXHR.responseText).Message;
                        } catch (e) { }

                        bootbox.confirm(message, 'Cancel', 'Manage Integrations', (result: boolean) => {
                            if (result)
                                window.location.href = '/project/' + App.selectedProject().id + '/manage#integrations';
                        });

                        return;
                    }

                    App.showErrorNotification('An error occurred while promoting the error stack.');
                });
        }

        public updateFixedStatus() {
            var url = StringUtil.format('/api/v1/stack/{id}', { id: this._errorStackId });
            this.patch(url, { DateFixed: !this.isFixed() ? DateUtil.now.toDate() : null },
                (data) => {
                    this.fixedOn(!this.isFixed() ? moment().toDate() : null);
                    if (this.isRegressed() && this.isFixed())
                        this.isRegressed(false);

                    if (this.isFixed())
                        App.showSuccessNotification('Successfully marked the error stack as fixed.');
                    else
                        App.showInfoNotification('Successfully marked the error stack as not fixed.');
                },
                () => {
                    if (this.isFixed())
                        App.showErrorNotification('An error occurred while marking this error stack as not fixed.');
                    else
                        App.showErrorNotification('An error occurred while marking this error stack as fixed.');
                });
        }

        public updateIsHidden() {
            var url = StringUtil.format('/api/v1/stack/{id}', { id: this._errorStackId });
            this.patch(url, { IsHidden: !this.isHidden() },
                (data) => {
                    this.isHidden(!this.isHidden());

                    if (this.isHidden())
                        App.showSuccessNotification('Error stack occurrences will be hidden from statistics and reports.');
                    else
                        App.showSuccessNotification('Error stack occurrences will be shown in statistics and reports.');
                },
                () => {
                    if (this.isHidden())
                        App.showErrorNotification('An error occurred while marking this error stack as hidden.');
                    else
                        App.showErrorNotification('An error occurred while marking this error stack as shown.');
                });
        }

        public updateNotificationSetting() {
            var isDisabled = !this.disableNotifications();
            var isCritical = isDisabled && this.occurrencesAreCritical() ? false : this.occurrencesAreCritical();

            var url = StringUtil.format('/api/v1/stack/{id}', { id: this._errorStackId });
            this.patch(url, { DisableNotifications: isDisabled, OccurrencesAreCritical: isCritical },
                (data) => {
                    this.disableNotifications(isDisabled);
                    this.occurrencesAreCritical(isCritical);
                    App.showSuccessNotification('Successfully updated the error stack notification settings.');
                },
                'An error occurred while saving the error stack notification settings.');
        }

        public updateOccurrencesAreCritical() {
            var isCritical = !this.occurrencesAreCritical();
            var isDisabled = isCritical && this.disableNotifications() ? false : this.disableNotifications();

            var url = StringUtil.format('/api/v1/stack/{id}', { id: this._errorStackId });
            this.patch(url, { DisableNotifications: isDisabled, OccurrencesAreCritical: isCritical },
                (data) => {
                    this.disableNotifications(isDisabled);
                    this.occurrencesAreCritical(isCritical);
                    App.showSuccessNotification('Successfully updated the future occurrences are critical settings.');
                },
                'An error occurred while saving the future occurrences are critical settings.');
        }

        public addReferenceLink() {
            this.url('');
            this.url.isModified(false);
            $('#add-new-reference-modal').modal({ backdrop: 'static', keyboard: true, show: true });
        }

        public removeReferenceLink(reference: string) {
            App.showConfirmDangerDialog('Are you sure you want to remove this reference link?', 'REMOVE REFERENCE LINK', result => {
                if (!result)
                    return;
                
                var data: string[] = ko.utils.arrayFilter(this.references(), (item: string) => item !== reference);
                this.patch(StringUtil.format('/api/v1/stack/{id}', { id: this._errorStackId }), { References: data },
                    () => {
                        this.references.remove(reference);
                        App.showSuccessNotification('Successfully removed the external reference link.');
                    },
                    'An error occurred while removing the external reference link.');
            });
        }

        public resetOccurrences() {
            var message = 'Are you sure you want to reset all error occurrences for this stack?';
            App.showConfirmDangerDialog(message, 'RESET ALL OCCURRENCE DATA', result => {
                if (!result)
                    return;

                var url = StringUtil.format('/api/v1/stack/{id}/resetdata', { id: this._errorStackId });
                this.retrieve(url, (data) => {
                    App.showSuccessNotification('Successfully reset the error stacks statistics and occurrences data.');
                    this.refreshViewModelData();
                    this._pagedErrorsByErrorStackIdViewModel.retrieve(this._pagedErrorsByErrorStackIdViewModel.retrieveResource);
                },
                'An error occurred while resetting the error stacks statistics and occurrences data.'); // TODO: This request should just return the stack results.
            });
        }

        public applyBindings() {
            this.removeReferenceLink = <any>this.removeReferenceLink.bind(this);

            super.applyBindings();
        }

        public refreshViewModelData() {
            this.retrieve('/api/v1/stack/' + this._errorStackId, (data) => {
                delete data.TotalOccurrences;
                this.populateViewModel(data);
            });

            this.retrieve(this.retrieveResource);
        }

        public get canRetrieve(): boolean {
            if (!this.filterViewModel)
                return false;
            
            return true;
        }

        public get retrieveResource(): string {
            if (!this.filterViewModel)
                return null;

            var url = this.baseUrl;
          
            var range: models.DateRange = this.filterViewModel.selectedDateRange();
            if (range.start())
                url = DataUtil.updateQueryStringParameter(url, 'start', DateUtil.formatISOString(range.start()));

            if (range.end())
                url = DataUtil.updateQueryStringParameter(url, 'end', DateUtil.formatISOString(range.end()));

            return url;
        }
    }
}