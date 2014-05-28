/// <reference path="../exceptionless.ts" />

module exceptionless.project {
    export class ManageViewModel extends ViewModelBase {
        private _projectId: string;
        private _navigationViewModel: NavigationViewModel;
        private _currentTimeout: number = 0;

        name: any = ko.observable<string>('').extend({ required: true });
        userId = ko.observable<string>('');
        mode = ko.observable<number>(0);
        sendDailySummary = ko.observable<boolean>(false);
        reportCriticalErrors = ko.observable<boolean>(false);
        reportRegressions = ko.observable<boolean>(false);
        report404Errors = ko.observable<boolean>(false);
        reportKnownBotErrors = ko.observable<boolean>(false);
        releases = ko.observableArray<any>([]);
        apiKeys = ko.observableArray<string>([]);
        configuration = ko.observableArray<models.KeyValuePair>([]);
        dataExclusions = ko.observable<string>('');

        url = ko.observable<string>('').extend({ required: true, url: true });
        eventTypes = ko.observableArray<string>([]).extend({
            validation: {
                validator: function (eventTypes) { return eventTypes.length > 0; },
                message: "Atlease one event type must be selected."
            }
        });
        projectHooks = ko.observableArray<models.ProjectHook>([]);

        customContent = ko.observable<string>('');

        saveDirtyFlag: any;
        saveCommand: KoliteCommand;

        saveConfigurationSettingsCommand: KoliteCommand;

        saveNotificationSettingsCommand: KoliteCommand;
        notificationSettingsDirtyFlag: any;
        
        saveProjectHookCommand: KoliteCommand;

        resetDataCommand: KoliteCommand;

        constructor (elementId: string, navigationElementId: string, projectId: string, tabElementId: string, data: JSON) {
            super(elementId, '/project', false);

            this._projectId = projectId;
            this._navigationViewModel = new NavigationViewModel(navigationElementId, null, projectId);
            TabUtil.init(tabElementId);

            App.selectedPlan.subscribe((plan: account.BillingPlan) => {
                $('#free-plan-notification').hide();
                if (plan.id === Constants.FREE_PLAN_ID)
                    $('#free-plan-notification').show();
            });

            App.selectedOrganization.subscribe(organization => {
                if (organization.isOverHourlyLimit)
                    $('#hourly-limit-notification').show();
                else
                    $('#hourly-limit-notification').hide();


                if (organization.isOverHourlyLimit)
                    $('#monthly-limit-notification').show();
                else
                    $('#monthly-limit-notification').hide();
            });

            this.saveDirtyFlag = new ko.DirtyFlag([this.name, this.customContent]);
            this.saveCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    return !isExecuting && this.saveDirtyFlag().isDirty() && $('form').valid();
                },
                execute: (complete) => {
                    var url = StringUtil.format('{url}/{id}', { url: this.baseUrl, id: this._projectId });
                    this.patch(url, { Name: this.name(), CustomContent: this.customContent() },
                        (data) => {
                            this.saveDirtyFlag().reset();
                            App.showSuccessNotification('Successfully saved the project name.');
                            complete();
                        }, () => {
                            App.showErrorNotification('An error occurred while saving the project name.');
                            complete();
                    });
                }
            });

            this.notificationSettingsDirtyFlag = new ko.DirtyFlag([this.mode, <any>this.sendDailySummary, this.reportCriticalErrors, this.reportRegressions, this.report404Errors, this.reportKnownBotErrors]);
            this.saveNotificationSettingsCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    return !isExecuting && this.notificationSettingsDirtyFlag().isDirty() && $('form').valid();
                },
                execute: (complete) => {
                    var url = StringUtil.format('{url}/{id}/notification/{userId}', { url: this.baseUrl, id: this._projectId, userId: this.userId() });
                    this.update(url, {
                            Mode: this.mode(),
                            SendDailySummary: this.sendDailySummary(),
                            ReportCriticalErrors: this.reportCriticalErrors(),
                            ReportRegressions: this.reportRegressions(),
                            Report404Errors: this.report404Errors(),
                            ReportKnownBotErrors: this.reportKnownBotErrors()
                        }, (data) => {
                            this.notificationSettingsDirtyFlag().reset();
                            App.showSuccessNotification('Successfully saved the notification settings.');
                            complete();
                        }, () => {
                            App.showErrorNotification('An error occurred while saving the notification settings.');
                            complete();
                        });
                }
            });

            this.saveConfigurationSettingsCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    return !isExecuting && ko.utils.arrayFirst(this.configuration(), (item: models.KeyValuePair) => !item.isValid) === null;
                },
                execute: (complete) => {
                    var data = { Configuration: { Settings: {} } };
                    ko.utils.arrayForEach(this.configuration(), (item: models.KeyValuePair) => {
                        if (!item.isValid)
                            return;

                        if (data[item.key()] === undefined)
                            data.Configuration.Settings[item.key()] = item.value();
                        else
                            App.showErrorNotification('Unable to save duplicate configuration key: ' + item.key());
                    });

                    if (!StringUtil.isNullOrEmpty(this.dataExclusions()))
                        data.Configuration.Settings['@@DataExclusions'] = this.dataExclusions();

                    var url = StringUtil.format('{url}/{projectId}', { url: this.baseUrl, projectId: this._projectId });
                    this.patch(url, data, (data) => {
                        App.showSuccessNotification('Successfully saved the configuration settings.');
                        complete();
                    }, () => {
                        App.showErrorNotification('An error occurred while saving the configuration settings.');
                        complete();
                    });
                }
            });

            this.resetDataCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    return !isExecuting;
                },
                execute: (complete) => {
                    App.showConfirmDangerDialog('Are you sure you want to reset the data for this project?', 'RESET PROJECT DATA', result => {
                        if (!result) {
                            complete();
                            return;
                        }

                        var url = StringUtil.format('{url}/{projectId}/resetdata', { url: this.baseUrl, projectId: this._projectId });
                        this.retrieve(url, (data) => {
                            this.notificationSettingsDirtyFlag().reset();
                            App.showSuccessNotification('Successfully reset project data.');
                            complete();
                        }, () => {
                            App.showErrorNotification('An error occurred while resetting project data.');
                            complete();
                        });
                    });
                }
            });

            this.saveProjectHookCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    if (isExecuting)
                        return false;
                    
                    return true;
                },
                execute: (complete) => {
                    if (!this.url.isValid() || !this.eventTypes.isValid()) {
                        this.errors.showAllMessages();
                        complete();
                        return;
                    }

                    this.insert({ ProjectId: this._projectId, Url: this.url(), EventTypes: this.eventTypes() }, '/api/v1/projecthook', (data) => {
                        $("#add-new-item-modal").modal('hide');

                        this.projectHooks.push(new models.ProjectHook(data.Id, data.ProjectId, data.Url, data.EventTypes));
                        App.showSuccessNotification('Successfully created Web Hook!');
                        complete();
                    }, (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                        if (jqXHR.status === 426) {
                            complete();
                            $("#add-new-item-modal").modal('hide');

                            var message = jqXHR.responseText;
                            try {
                                message = JSON.parse(jqXHR.responseText).Message;
                            } catch (e) { }

                            bootbox.confirm(message, 'Cancel', 'Upgrade Plan', (result: boolean) => {
                                if (result)
                                    App.showChangePlanDialog();
                            });

                            return;
                        }
                        
                        App.showErrorNotification('An error occurred while creating the Web Hook');
                        complete();
                    });
                }
            });
            
            this.errors = ko.validation.group(this, { deep: true });
            this.populateViewModel(data);
            this.applyBindings();
            
            this.refreshViewModelData();
        }

        public refreshViewModelData() {
            this.retrieve('/api/v1/projecthook/project/' + this._projectId + '/', (data: any) => {
                this.projectHooks.push(new models.ProjectHook(data.Id, data.ProjectId, data.Url, data.EventTypes));
                var projectHooks: models.ProjectHook[] = [];
                for (var i = 0; i < data.length; i++) {
                    projectHooks.push(new models.ProjectHook(data[i].Id, data[i].ProjectId, data[i].Url, data[i].EventTypes));
                }
                this.projectHooks(projectHooks);
            });
        }

        public populateViewModel(data?: any) {
            this.name(data.Name);
            this.name.isModified(false);
            this.customContent(data.CustomContent);

            this.mode(data.Mode);
            this.sendDailySummary(data.SendDailySummary);
            this.reportCriticalErrors(data.ReportCriticalErrors);
            this.reportRegressions(data.ReportRegressions);
            this.report404Errors(data.Report404Errors);
            this.reportKnownBotErrors(data.ReportKnownBotErrors);
            this.notificationSettingsDirtyFlag().reset();

            if (data.UserId)
                this.userId(data.UserId);

            var apiKeys: string[] = [];
            for (var i = 0; i < data.ApiKeys.length; i++) {
                apiKeys.push(data.ApiKeys[i]);
            }
            this.apiKeys(apiKeys);

            var settings: models.KeyValuePair[] = [];
            for (var name in data.Configuration.Settings) {
                if (name === '@@DataExclusions') {
                    this.dataExclusions(data.Configuration.Settings['@@DataExclusions']);
                    continue;
                }

                var setting = new models.KeyValuePair(name, data.Configuration.Settings[name]);
                setting.key.extend({
                    required: true,
                    unique: {
                        collection: ko.utils.arrayFilter(this.configuration(), (item) => { return item.id() !== setting.id(); }),
                        valueAccessor: (kvp: models.KeyValuePair) => kvp.key(),
                        externalValue: ''
                    }
                });

                settings.push(setting);
            }

            this.configuration(settings);
        }

        public get hasPremiumFeatures(): KnockoutComputed<boolean> {
            return ko.computed(() => { return App.selectedPlan().hasPremiumFeatures; }, this);
        }

        public addProjectHook() {
            this.url('');
            this.url.isModified(false);
            this.eventTypes.removeAll();
            this.eventTypes.isModified(false);
            $('#add-new-item-modal').modal({ backdrop: 'static', keyboard: true, show: true });
        }

        public removeProjectHook(projectHook: models.ProjectHook) {
            App.showConfirmDangerDialog('Are you sure you want to delete this Web Hook?', 'DELETE WEB HOOK', result => {
                if (!result)
                    return;

                this.remove('/api/v1/projecthook/' + projectHook.id, (data) => {
                    App.showSuccessNotification('Successfully deleted Web Hook!');
                    this.projectHooks.remove(projectHook);
                }, 'An error occurred while deleting the Web Hook.');
            });
        }

        public addConfigurationValue() {
            var canAdd = ko.utils.arrayFirst(this.configuration(), (k: models.KeyValuePair) => { return !k.isValid; }) === null;
            if (!canAdd)
                return;

            var setting = new models.KeyValuePair('', '');
            setting.key.extend({
                required: true,
                unique: {
                    collection: ko.utils.arrayFilter(this.configuration(), (item) => { return item.id() !== setting.id(); }),
                    valueAccessor: (kvp: models.KeyValuePair) => kvp.key(),
                    externalValue: ''
                } });

            this.configuration.push(setting);
        }

        public removeConfigurationValue(kvp: models.KeyValuePair) {
            this.configuration.remove(kvp);
        }

        public addApiKey() {
            this.insert(null, StringUtil.format('{url}/{projectId}/key/', { url: this.baseUrl, projectId: this._projectId }),
                (data) => {
                    this.apiKeys.push(data);
                    App.showSuccessNotification('Successfully created a new API key.');
                },
                'An error occurred while creating a new API key for your project.');
        }

        public glueZeroClipboard(elements: any, data: any) {
            App.initZeroClipboard();
        }

        public removeApiKey(key: string) {
            App.showConfirmDangerDialog('Are you sure you want to remove the API key?', 'REMOVE API KEY', result => {
                if (!result)
                    return;

                var url = StringUtil.format('{url}/{projectId}/key/{key}', { url: this.baseUrl, projectId: this._projectId, key: key });
                this.remove(url, (data) => {
                    this.apiKeys.remove(key);
                    App.showSuccessNotification('Successfully removed the API key.');
                }, 'An error occurred while trying to remove the API Key.');
            });
        }

        public applyBindings() {
            this.removeConfigurationValue = <any>this.removeConfigurationValue.bind(this);
            this.removeApiKey = <any>this.removeApiKey.bind(this);
            this.removeProjectHook = <any>this.removeProjectHook.bind(this);
            
            super.applyBindings();
        }
    }
}