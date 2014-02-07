/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class FilterViewModel extends ViewModelBase {
        dateRanges = ko.observableArray<models.DateRange>([]);
        selectedDateRange = ko.observable<models.DateRange>();
        showHidden = ko.observable<boolean>(false);
        showFixed = ko.observable<boolean>(false);
        showNotFound = ko.observable<boolean>(true);
        
        constructor (elementId: string) {
            super(elementId);
            
            this.selectedDateRange.subscribe((value: models.DateRange) => {
                localStorage.setItem(Constants.FILTER, value.id());
                localStorage.setItem(Constants.START, value.start() ? value.start().format() : null);
                localStorage.setItem(Constants.END, value.end() ? value.end().format() : null);
            });

            this.showHidden.subscribe((value: boolean) => localStorage.setItem(Constants.SHOW_HIDDEN, value ? 'true' : 'false' ));
            this.showFixed.subscribe((value: boolean) => localStorage.setItem(Constants.SHOW_FIXED, value ? 'true' : 'false'));
            this.showNotFound.subscribe((value: boolean) => localStorage.setItem(Constants.SHOW_NOTFOUND, value ? 'true' : 'false'));
            
            $('#start').datepicker({
                startDate: new Date(2013, 0, 0),
                endDate: moment().startOf('day').toDate(),
                todayBtn: 'linked'
            }).on('changeDate', (ev: any) => {
                var endDate = $('#end').datepicker('getDate');
                if (endDate < ev.date)
                    $('#end').datepicker('update', DateUtil.formatWithFriendlyMonthDayYear(moment.utc(ev.date)));
            });

            $('#end').datepicker({
                startDate: new Date(2013, 0, 0),
                endDate: moment().startOf('day').toDate(),
                todayBtn: 'linked'
            }).on('changeDate', (ev: any) => {
                var startDate = $('#start').datepicker('getDate');
                if (startDate > ev.date)
                    $('#start').datepicker('update', DateUtil.formatWithFriendlyMonthDayYear(moment.utc(ev.date)));
            });

            $('#custom-date-range-modal').on('show', () => {
                var start = this.selectedDateRange().start() != null ? this.selectedDateRange().start() : moment.utc().startOf('month');
                var end = this.selectedDateRange().end() != null ? this.selectedDateRange().end() : moment.utc().startOf('day');

                $('#start').datepicker('update', DateUtil.formatWithFriendlyMonthDayYear(start));
                $('#end').datepicker('update', DateUtil.formatWithFriendlyMonthDayYear(end));
            });

            window.addEventListener('popstate', (ev: PopStateEvent) => { if (ev.state && !ev.state.internal) this.navigate(ev); });
            
            this.populateViewModel();
            this.applyBindings();
        }

        public populateViewModel(data?: any) {
            this.dateRanges.push(new models.Last24HoursDateRange());
            this.dateRanges.push(new models.LastWeekDateRange());
            this.dateRanges.push(new models.Last30DaysDateRange());
            this.dateRanges.push(new models.AllTimeDateRange());
            
            var selectedRange: models.DateRange;
            var customRange = this.getCustomDateRange();
            var filter: string = DataUtil.getValue(Constants.FILTER);
            if (!StringUtil.isNullOrEmpty(filter)) {
                if (filter === Constants.CUSTOM) {
                    if (customRange)
                        selectedRange = customRange;
                } else {
                    selectedRange = ko.utils.arrayFirst(this.dateRanges(), (range: models.DateRange) => { return range.id() === filter; });
                }
            }

            var range: models.DateRange = selectedRange ? selectedRange : this.dateRanges()[2];
            this.selectedDateRange(range);

            var state = $.extend(history.state ? history.state : {}, { range: { id: range.id(), friendlyName: range.friendlyName(), start: range.start() ? range.start().format() : null, end: range.end() ? range.end().format() : null } });
            history.replaceState(state, range.friendlyName(), this.updateNavigationUrl());

            var hidden = DataUtil.getValue(Constants.SHOW_HIDDEN);
            this.showHidden(hidden == 'true' ? true : false);

            var fixed = DataUtil.getValue(Constants.SHOW_FIXED);
            this.showFixed(fixed == 'true' ? true : false);

            var notfound = DataUtil.getValue(Constants.SHOW_NOTFOUND);
            this.showNotFound(notfound == 'false' ? false : true);
        }

        public applyBindings() {
            this.changeDateRange = <any>this.changeDateRange.bind(this);
            this.applyCustomRange = <any>this.applyCustomRange.bind(this);
            
            super.applyBindings();
        }

        private navigate(ev: PopStateEvent) {
            if (ev.state && ev.state.range) {
                var range: models.DateRange;
                switch (ev.state.range.id) {
                    case Constants.LAST_24_HOURS:
                        range = this.dateRanges()[0];
                        break;
                    case Constants.LAST_WEEK:
                        range = this.dateRanges()[1];
                        break;
                    case Constants.LAST_30_DAYS:
                        range = this.dateRanges()[2];
                        break;
                    case Constants.ALL:
                        range = this.dateRanges()[3];
                        break;
                    default:
                        range = new models.DateRange(ev.state.range.id, ev.state.range.friendlyName, moment.utc(ev.state.range.start), ev.state.range.end ? moment.utc(ev.state.range.end) : null);
                        break;
                }

                if ((range.id() !== this.selectedDateRange().id()) || (range.id() === Constants.CUSTOM && range.start() !== this.selectedDateRange().start() && Constants.CUSTOM && range.end() !== this.selectedDateRange().end()))
                    this.selectedDateRange(range);
            }
        }

        private updateNavigationUrl(): string {
            return location.pathname + location.hash + location.search;
        }

        private getCustomDateRange(): models.DateRange {
            var start: string = DataUtil.getValue(Constants.START);
            var end: string = DataUtil.getValue(Constants.END);

            // TODO: if the query string doesn't contain an hour range, then set this to start and end of day.
            if (!StringUtil.isNullOrEmpty(start) && !StringUtil.isNullOrEmpty(end))
                return new models.DateRange(Constants.CUSTOM, 'Custom', moment.utc(start), moment.utc(end));

            return null;
        }

        private applyCustomRange() {
            $('#custom-date-range-modal').modal('hide');

            var startDate = $('#start').datepicker('getDate');
            var endDate = $('#end').datepicker('getDate');

            this.changeDateRange(new models.DateRange(Constants.CUSTOM, 'Custom', moment.utc(<any>startDate).startOf('day'), moment.utc(<any>endDate).endOf('day')));
        }

        public changeDateRange(range: models.DateRange) {
            this.selectedDateRange(range);

            var state = $.extend(history.state ? history.state : {}, { range: { id: range.id(), friendlyName: range.friendlyName(), start: range.start() ? range.start().format() : null, end: range.end() ? range.end().format() : null } });
            history.pushState(state, range.friendlyName(), this.updateNavigationUrl());
        }
    }
}