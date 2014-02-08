/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class DateUtil {
        constructor () { }
        
        public static timeZoneOffset = ko.observable<number>(0);

        public static get minValue(): Moment {
            return moment.utc(Constants.UTC_MIN_VALUE);
        }

        public static get now(): Moment {
            return moment.utc().add('milliseconds', DateUtil.timeZoneOffset());
        }

        public static parse(date: any): Date {
            return moment(date).toDate();
            //return moment.utc(date).add('milliseconds', timeZoneOffset).toDate();
        }
        
        public static get localTimeZoneOffset(): string {
            return moment().format(Constants.TIMEZONE_FORMAT);
        }

        public static format(moment: Moment): string {
            return moment.format(Constants.MONTH_DAY_YEAR_TIME_FORMAT);
        }

        public static formatISOString(moment: Moment): string {
            return moment.format(Constants.ISO_STRING_WITHOUT_TIMEZONE);
        }

        public static formatWithMonthDay(moment: Moment): string {
            return moment.format(Constants.MONTH_DAY_FORMAT);
        }

        public static formatWithMonthDayYear(moment: Moment): string {
            return moment.format(Constants.MONTH_DAY_YEAR_FORMAT);
        }

        public static formatWithFullDateAndTime(moment: Moment): string {
            return moment.format(Constants.FULL_DATE_AND_TIME_FORMAT);
        }

        public static formatWithFriendlyFullDate(moment: Moment): string {
            return moment.format(Constants.FRIENDLY_FULL_DATE_FORMAT);
        }

        public static formatWithFriendlyMonthDayYear(moment: Moment): string {
            return moment.format(Constants.FRIENDLY_MONTH_DAY_YEAR_FORMAT);
        }

        public static formatWithFriendlyMobileMonthDayYear(moment: Moment): string {
            return moment.format(Constants.FRIENDLY_MOBILE_MONTH_DAY_YEAR_FORMAT);
        }
    }
}