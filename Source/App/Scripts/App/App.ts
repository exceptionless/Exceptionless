/// <reference path="exceptionless.ts" />

// This code should run on every page.
module exceptionless {
    export class App {
        public static onResize = ko.observable($(window).width());
        public static isPhoneLayout = ko.observable(false);
        public static isTabletLayout = ko.observable(false);
        public static isDesktopLayout = ko.observable(false);

        public static onPlanChanged = ko.observable<{ organizationId: string; }>();
        public static onOrganizationUpdated = ko.observable<{ organizationId: string; }>();
        public static onProjectUpdated = ko.observable<{ projectId: string; }>();
        public static onStackUpdated = ko.observable<{ stackId: string; }>();
        public static onErrorOccurred = ko.observable<{ projectId: string; }>();

        public static organizations = ko.observableArray<models.Organization>([]);
        private static _previousOrganization: models.Organization;

        public static projects = ko.observableArray<models.ProjectInfo>([]);
        public static selectedProject = ko.observable<models.ProjectData>(new models.ProjectInfo('', 'Loading...', '', DateUtil.timeZoneOffset(), 0, 0, 0));
        public static user = ko.observable<models.User>(new models.User(null, 'Loading...', '', false, false, false));

        public static enableBilling = ko.observable(true);
        public static plans = ko.observableArray<account.BillingPlan>([]);

        public static loading = ko.observable<boolean>(false);

        constructor() {}

        public static init() {
            App.loading(true);

            ko.validation.rules['url'] = {
                validator: function (val, validate) {
                    return ko.validation.utils.isEmptyVal(val) || validate && val.match(/(http|https):\/\/(\w+:{0,1}\w*@)?(\S+)(:[0-9]+)?(\/|\/([\w#!:.?+=&%@!\-\/]))?/) != null;
                },
                message: "Please enter a valid URL"
            };

            ko.validation.configure({
                insertMessages: true,
                decorateElement: true,
                parseInputAttributes: true,
                errorElementClass: 'error',
                errorMessageClass: 'help-inline',
            });

            var validator = $('form').data('validator');
            if (validator)
                validator.settings.onkeyup = false;

            App.addBindingHandlers();
            
            window.addEventListener('hashchange', (hashChangeEvent: any) => App.updateTrunk8());

            App.updateTrunk8();
            App.updateResponsiveLayouts($(window).width());
            $(window).resize(e => {
                var width = $(window).width();
                if (App.onResize() === width)
                    return;

                App.onResize(width);

                App.updateResponsiveLayouts(width);
                App.updateTrunk8();
            });

            ZeroClipboard.config({ moviePath: '/scripts/zeroclipboard.swf' });

            moment.lang('en', {
                relativeTime: {
                    future: "in %s",
                    past: "%s ago",
                    s: "seconds",
                    m: "a minute",
                    mm: "%d minutes",
                    h: "an hour",
                    hh: "%d hours",
                    d: "a day",
                    dd: "%d days",
                    M: "a month",
                    MM: "%d months",
                    y: "a year",
                    yy: "%d years"
                }
            });

            (<any>App).selectedOrganization.subscribe(o => App._previousOrganization = o, null, 'beforeChange');
            (<any>App).selectedOrganization.subscribe((o)=> {
                if (App._previousOrganization
                    && !StringUtil.isNullOrEmpty(App._previousOrganization.id)
                    && o
                    && !StringUtil.isNullOrEmpty(o.id)
                    && (App._previousOrganization.id === o.id)
                    && (App._previousOrganization.isSuspended !== o.isSuspended)) {
                    location.reload();
                }
            });

            App.onPlanChanged.subscribe(() => App.refreshViewModelData());
            App.onOrganizationUpdated.subscribe(() => App.refreshViewModelData());
            App.onProjectUpdated.subscribe(() => App.refreshViewModelData());

            App.refreshViewModelData();

            setTimeout(App.startSignalr, 1000);
        }

        public static initZeroClipboard() {
            var clip = new ZeroClipboard($("button.clipboard"));
            clip.on('noflash wrongflash', () => $("button.clipboard").hide());
            clip.on('complete', (client, text) => App.showSuccessNotification('Copied!'));
        }

        public static refreshViewModelData() {
            if(!App.loading())
                App.loading(true);

            var url = '/account/init';
            var projectId = DataUtil.getProjectId();
            if (!StringUtil.isNullOrEmpty(projectId))
                url = DataUtil.updateQueryStringParameter(url, 'projectId', projectId);

            var organizationId = DataUtil.getOrganizationId();
            if (!StringUtil.isNullOrEmpty(organizationId))
                url = DataUtil.updateQueryStringParameter(url, 'organizationId', organizationId);

            $.ajax(url, {
                dataType: 'json',
                success: (data: any) => App.populateViewModel(data),
                error: () => App.showErrorNotification('An error occurred while retrieving profile information.'),
                complete: () => App.loading(false)
            });
        }

        private static populateViewModel(data: any) {
            App.user(new models.User(data.User.Id, data.User.FullName, data.User.EmailAddress, data.User.IsEmailAddressVerified, data.User.IsInvite, data.User.HasAdminRole));

            App.enableBilling(data.EnableBilling);

            var plans: account.BillingPlan[] = [];
            $.each(data.BillingInfo, (index, p) => plans.push(new account.BillingPlan(p.Id, p.Name, p.Description, p.Price, p.HasPremiumFeatures, p.MaxProjects, p.MaxErrors, p.MaxPerStack, p.MaxUsers, p.StatRetention, p.IsHidden)));
            App.plans(plans);
            App.plans.sort((a: account.BillingPlan, b: account.BillingPlan) => { return a.price > b.price ? 1 : -1; });

            var organizations: models.Organization[] = [];
            $.each(data.Organizations, (index, o) => organizations.push(new models.Organization(o.Id, o.Name, o.ProjectCount, o.StackCount, o.ErrorCount, o.TotalErrorCount, o.LastErrorDate, o.SubscribeDate, o.BillingChangeDate, o.BillingChangedByUserId, o.BillingStatus, o.BillingPrice, o.PlanId, o.CardLast4, o.StripeCustomerId, o.IsSuspended, o.SuspensionCode, o.SuspensionDate, o.SuspendedByUserId, o.SuspensionNotes)));
            App.organizations(organizations);
            App.organizations.sort((a: models.Organization, b: models.Organization) => { return a.name.toLowerCase() > b.name.toLowerCase() ? 1 : -1; });

            var projects: models.ProjectInfo[] = [];
            $.each(data.Projects, (index, p) => projects.push(new models.ProjectInfo(p.Id, p.Name, p.OrganizationId, p.TimeZoneOffset, p.StackCount, p.ErrorCount, p.TotalErrorCount)));
            App.projects(projects);
            App.projects.sort((a: models.ProjectInfo, b: models.ProjectInfo) => { return a.name.toLowerCase() > b.name.toLowerCase() ? 1 : -1; });

            if (StringUtil.isNullOrEmpty(App.selectedProject().id)) {
                var project = ko.utils.arrayFirst(App.projects(), (project: models.Project)=> project.id === DataUtil.getProjectId());
                if (!project && App.projects().length > 0)
                    project = App.projects()[0];

                App.selectedProject(<models.ProjectData>project);
            }
        }

        public static get selectedPlan(): KnockoutComputed<account.BillingPlan> {
            return ko.computed<account.BillingPlan>(() => App.selectedOrganization().selectedPlan);
        }

        public static get selectedOrganization(): KnockoutComputed<models.Organization> {
            return ko.computed(() => App.selectedProject() && App.selectedProject().organization ? App.selectedProject().organization : new models.Organization('', 'Loading...', 0, 0, 0, 0));
        }

        public static showChangePlanDialog(org?: { id: string; name: string; planId: string }) {
            if (!App.enableBilling()) {
                App.showErrorNotification('Plans cannot be changed while billing is disabled.');
                return;
            }

            if (!org || StringUtil.isNullOrEmpty(org.id))
                org = App.selectedOrganization();

            // Detect if the current organization exists.
            if (ko.utils.arrayFirst(App.organizations(), o=> o.id === org.id) == null) {
                if (org instanceof models.Organization)
                    App.organizations.push(<models.Organization>org);
                else
                    App.organizations.push(new models.Organization(org.id, org.name, 0, 0, 0, 0, new Date(), new Date(), new Date(), '', 0, 0, org.name, '', ''));
            }

            $('#plan-modal').data('organizationId', org.id).modal('show');
        }

        public static showConfirmDangerDialog(message: string, dangerButtonText: string, callback: (result: boolean) => void) {
            bootbox.dialog(message, <any>[{
                'label': dangerButtonText,
                'class': 'btn-danger',
                'callback': function () { callback(true); }
            }, {
                'label': 'Cancel',
                'class': 'btn btn-primary',
                'callback': function () { callback(false); }
            }]);
        }

        public static showSuccessNotification(message: string, title?: string, options?: ToastrOptions) {
            options = $.extend(options, {
                "positionClass": "toast-bottom-right",
                "fadeOut": 3000,
                "extendedTimeOut": 1000
            });

            toastr.success(message, title, options);
        }

        public static showInfoNotification(message: string, title?: string, options?: ToastrOptions) {
            options = $.extend(options, {
                "positionClass": "toast-bottom-right",
                "fadeOut": 3000,
                "extendedTimeOut": 1000
            });

            toastr.info(message, title, options);
        }

        public static showErrorNotification(message: string, title?: string, options?: ToastrOptions) {
            options = $.extend(options, {
              "positionClass": "toast-bottom-right",
              "fadeOut": 5000,
              "extendedTimeOut": 1000
            });
            toastr.error(message, title, options);
        }

        private static addBindingHandlers() {
            ko.bindingHandlers['formatNumber'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<number>(valueAccessor());
                    if (!isNaN(value))
                        $(element).text(numeral(value).format('0,0[.]0'));
                    else
                        $(element).text(value);
                }
            };

            ko.virtualElements.allowedBindings['formatNumber'] = true;

            ko.bindingHandlers['formatCurrency'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<number>(valueAccessor());
                    if (!isNaN(value))
                        $(element).text(numeral(value).format('$0,0[.]0'));
                    else
                        $(element).text(value);
                }
            };

            ko.virtualElements.allowedBindings['formatCurrency'] = true;

            ko.bindingHandlers['formatDate'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<Date>(valueAccessor());
                    if (value)
                        $(element).text(DateUtil.format(moment(value)));
                    else
                        $(element).text(<any>value);
                }
            };
            ko.virtualElements.allowedBindings['formatDate'] = true;

            // TODO We could combine these with optional binding arguments.
            ko.bindingHandlers['formatWithMonthDayYear'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<Date>(valueAccessor());
                    if (value)
                        $(element).text(DateUtil.formatWithMonthDayYear(moment(value)));
                    else
                        $(element).text(<any>value);
                }
            };
            ko.virtualElements.allowedBindings['formatWithMonthDayYear'] = true;

            // TODO We could combine these with optional binding arguments.
            ko.bindingHandlers['formatDateWithMonthDay'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<Date>(valueAccessor());
                    if (value)
                        $(element).text(DateUtil.formatWithMonthDay(moment(value)));
                    else
                        $(element).text(<any>value);
                }
            };
            ko.virtualElements.allowedBindings['formatDateWithMonthDay'] = true;

            // TODO We could combine these with optional binding arguments.
            ko.bindingHandlers['formatDateWithFullDateAndTime'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<Date>(valueAccessor());
                    if (value)
                        $(element).text(DateUtil.formatWithFullDateAndTime(moment(value)));
                    else
                        $(element).text(<any>value);
                }
            };
            ko.virtualElements.allowedBindings['formatDateWithFullDateAndTime'] = true;

            // TODO We could combine these with optional binding arguments.
            ko.bindingHandlers['formatTitleDateWithMonthDayYear'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<Date>(valueAccessor());
                    $(element).attr('title', (DateUtil.formatWithMonthDayYear(moment(value))));
                }
            };
            ko.virtualElements.allowedBindings['formatTitleDateWithMonthDayYear'] = true;

            ko.bindingHandlers['livestamp'] = {
                'update': function (element, valueAccessor, allBindingsAccessor) {
                    var value = ko.utils.unwrapObservable<Date>(valueAccessor());
                    if (value) {
                        var date = moment(value);
                        if (date.year() < 2012) {
                            $(element).text('Never').livestamp('destroy');
                        } else {
                            var allBindings = allBindingsAccessor();
                            if (!allBindings.hideTitle)
                                $(element).attr('title', DateUtil.format(date));

                            $(element).text(date.fromNow()).livestamp(date);
                        }
                    } else {
                        $(element).text(<any>value).livestamp('destroy');
                    }
                }
            };
            ko.virtualElements.allowedBindings['livestamp'] = true;

            // TODO We could combine these with optional binding arguments.
            ko.bindingHandlers['formatWithFriendlyFullDate'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<Date>(valueAccessor());
                    if (value)
                        $(element).text(DateUtil.formatWithFriendlyFullDate(moment(value)));
                    else
                        $(element).text(<any>value);
                }
            };
            ko.virtualElements.allowedBindings['formatWithFriendlyFullDate'] = true;

            // TODO We could combine these with optional binding arguments.
            ko.bindingHandlers['formatWithFriendlyMonthDayYear'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<Date>(valueAccessor());
                    if (value)
                        $(element).text(DateUtil.formatWithFriendlyMonthDayYear(moment(value)));
                    else
                        $(element).text(<any>value);
                }
            };
            ko.virtualElements.allowedBindings['formatWithFriendlyMonthDayYear'] = true;

            // TODO We could combine these with optional binding arguments.
            ko.bindingHandlers['customDateRangeFriendlyName'] = {
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<models.DateRange>(valueAccessor());
                    if (value) {
                        if (value.start().format() === (<any>value.start()).clone().startOf('day').format() && value.end().format() === (<any>value.end()).clone().endOf('day').format()) {
                            $(element).text((<any>value.start()).twix(value.end(), true).format());
                        } else {
                            $(element).text((<any>value.start()).twix(value.end()).format());
                        }
                    } else
                        $(element).text(<any>value);
                }
            };
            ko.virtualElements.allowedBindings['customDateRangeFriendlyName'] = true;
            
            ko.bindingHandlers['popover'] = {
                'init': function (element, valueAccessor, allBindingsAccessor, viewModel) {
                    var options = ko.utils.unwrapObservable(valueAccessor());
                    var defaultOptions = { trigger: 'hover', html: true, placement: 'top' };
                    options = $.extend(true, {}, defaultOptions, options);
                    $(element).popover(options);
                }
            };
            ko.virtualElements.allowedBindings['popover'] = true;

            ko.bindingHandlers['checkedButtons'] = {
                'init': function (element, valueAccessor, allBindingsAccessor) {
                    var type = element.getAttribute('data-toggle') || 'radio';
                    var updateHandler = function () {
                        var valueToWrite;
                        var isActive = !!~element.className.indexOf('active');
                        var dataValue = element.getAttribute('data-value');
                        if (type == "checkbox") {
                            valueToWrite = !isActive;
                        } else if (type == "radio" && !isActive) {
                            valueToWrite = dataValue;
                        } else {
                            return; // "checkedButtons" binding only responds to checkbox and radio data-toggle attribute
                        }

                        var modelValue = valueAccessor();
                        if ((type == "checkbox") && (ko.utils.unwrapObservable<any>(modelValue) instanceof Array)) {
                            // For checkboxes bound to an array, we add/remove the checkbox value to that array
                            // This works for both observable and non-observable arrays
                            var existingEntryIndex: number = ko.utils.arrayIndexOf(ko.utils.unwrapObservable<any>(modelValue), dataValue);
                            if (!isActive && (existingEntryIndex < 0))
                                modelValue.push(dataValue);
                            else if (isActive && (existingEntryIndex >= 0))
                                modelValue.splice(existingEntryIndex, 1);
                        } else {
                            if (modelValue() !== valueToWrite) {
                                modelValue(valueToWrite);
                            }
                        }
                    };

                    ko.utils.registerEventHandler(element, "click", updateHandler);
                },
                'update': function (element, valueAccessor) {
                    var value = ko.utils.unwrapObservable<any>(valueAccessor());
                    var type = element.getAttribute('data-toggle') || 'radio';

                    if (type == "checkbox") {
                        if (value instanceof Array) {
                            // When bound to an array, the checkbox being checked represents its value being present in that array
                            if (ko.utils.arrayIndexOf(value, element.getAttribute('data-value')) >= 0) {
                                ko.utils.toggleDomNodeCssClass(element, 'active', true);
                            }
                            else {
                                ko.utils.toggleDomNodeCssClass(element, 'active', false);
                            }

                        } else {
                            // When bound to anything other value (not an array), the checkbox being checked represents the value being trueish
                            ko.utils.toggleDomNodeCssClass(element, 'active', value);
                        }
                    } else if (type == "radio") {
                        ko.utils.toggleDomNodeCssClass(element, 'active', element.getAttribute('data-value') == value);
                    }
                }
            };

            ko.virtualElements.allowedBindings['checkedButtons'] = true;

            ko.bindingHandlers['trunk8'] = {
                'update': function (element, valueAccessor, allBindingsAccessor) {
                    var allBindings = allBindingsAccessor();
                    var lines = allBindings.lines || 1;

                    var value = ko.utils.unwrapObservable<string>(valueAccessor());
                    $(element).text(value).addClass(lines > 1 ? '.t8-lines' + lines : '.t8-default').trunk8({ lines: lines });
                }
            };
            ko.virtualElements.allowedBindings['trunk8'] = true;
        }

        private static updateTrunk8() {
            $('.t8-default').trunk8();
            $('.t8-lines2').trunk8({ lines: 2 });
            $('.t8-lines3').trunk8({ lines: 3 });
            $('.t8-lines4').trunk8({ lines: 4 });
        }

        private static updateResponsiveLayouts(width: number) {
            var responsiveClass = 'desktop not-phone not-tablet';
            if (width <= 767) {
                responsiveClass = 'phone not-tablet not-desktop';
                if (!App.isPhoneLayout()) {
                    App.isPhoneLayout(true);
                    App.isTabletLayout(false);
                    App.isDesktopLayout(false);
                }
            } else if (width >= 768 && width <= 979) {
                responsiveClass = 'tablet not-phone not-desktop';
                if (!App.isTabletLayout()) {
                    App.isPhoneLayout(false);
                    App.isTabletLayout(true);
                    App.isDesktopLayout(false);
                }
            } else if (!App.isDesktopLayout()) {
                App.isPhoneLayout(false);
                App.isTabletLayout(false);
                App.isDesktopLayout(true);
            }

            $(document.body).removeClass('not-phone phone not-tablet tablet not-desktop desktop').addClass(responsiveClass);
        }

        private static startSignalr() {
            $.ajax({ url: "/signalr/hubs", dataType: "script", async: false });

            if (!$.connection)
                return;

            var notifier = (<any>$.connection).notifier;
            //$.connection.hub.logging = true;

            notifier.client.planChanged = (organizationId: string) => {
                App.onPlanChanged({ organizationId: organizationId });
            };

            notifier.client.organizationUpdated = (organizationId: string) => {
                App.onOrganizationUpdated({ organizationId: organizationId });
            };

            notifier.client.projectUpdated = (projectId: string) => {
                App.onProjectUpdated({ projectId: projectId });
            };

            notifier.client.stackUpdated = (projectId: string, stackId: string) => {
                if (projectId === App.selectedProject().id)
                    App.onStackUpdated({ stackId: stackId });
            };

            notifier.client.newError = (projectId: string) => {
                if (projectId === App.selectedProject().id)
                    App.onErrorOccurred({ projectId: App.selectedProject().id });
            };

            $.connection.hub.start();
        }
    }
}

$(document).ready(r => exceptionless.App.init());