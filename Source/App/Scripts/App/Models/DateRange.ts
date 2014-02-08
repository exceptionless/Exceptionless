/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class DateRange {
        public id = ko.observable<string>('');
        public friendlyName = ko.observable<string>('');
        start: () => Moment;
        end: () => Moment;

        constructor(id: string, friendlyName: string, start?: Moment, end?: Moment) {
            this.id(id);
            this.friendlyName(friendlyName);

            this.start = () => start;
            this.end = () => end;
        }
    }

    export class Last24HoursDateRange extends DateRange {
        constructor() {
            super(Constants.LAST_24_HOURS, 'Last 24 Hours');

            this.start = () => DateUtil.now.subtract('hours', 24);
        }
    }

    export class LastWeekDateRange extends DateRange {
        constructor() {
            super(Constants.LAST_WEEK, 'Last Week');
            this.start = ko.computed(() => {
                return DateUtil.now.subtract('days', 7).startOf('day');
            }, this);
        }
    }

    export class Last30DaysDateRange extends DateRange {
        constructor() {
            super(Constants.LAST_30_DAYS, 'Last 30 Days');
            this.start = ko.computed(() => {
                return DateUtil.now.subtract('days', 30).startOf('day');
            }, this);
        }
    }

    export class AllTimeDateRange extends DateRange {
        constructor() {
            super(Constants.ALL, 'All Time');
        }
    }
}