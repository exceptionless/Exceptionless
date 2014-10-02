/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class Constants {
        constructor() { }

        public static get FREE_PLAN_ID(): string { return 'EX_FREE'; }

        public static get ORGANIZATION(): string { return 'organization'; }

        // ProjectListViewModel
        public static get PROJECT(): string { return 'project'; }
        public static get PROJECT_RECENT(): string { return 'recent'; }
        public static get PROJECT_FREQUENT(): string { return 'frequent'; }
        public static get PROJECT_NEW(): string { return 'new'; }

        // FilterViewModel
        public static get FILTER(): string { return 'filter'; }
        public static get SHOW_HIDDEN(): string { return 'hidden'; }
        public static get SHOW_FIXED(): string { return 'fixed'; }
        public static get SHOW_NOTFOUND(): string { return 'notfound'; }
        public static get CUSTOM(): string { return 'custom'; }
        public static get START(): string { return 'start'; }
        public static get END(): string { return 'end'; }
        public static get LAST_24_HOURS(): string { return 'last24'; }
        public static get LAST_WEEK(): string { return 'lastweek'; }
        public static get LAST_30_DAYS(): string { return 'last30'; }
        public static get ALL(): string { return 'all'; }

        // DateUtil
        public static get UTC_MIN_VALUE(): string { return '0001-01-01T00:00:00'; }
        public static get MONTH_DAY_FORMAT(): string { return 'MM-DD'; }
        public static get MONTH_DAY_YEAR_FORMAT(): string { return 'YYYY-' + Constants.MONTH_DAY_FORMAT; }

        public static get MONTH_DAY_YEAR_TIME_FORMAT(): string { return Constants.MONTH_DAY_YEAR_FORMAT + ' h:mm:ss a'; }
        public static get FULL_DATE_AND_TIME_FORMAT(): string { return Constants.FRIENDLY_FULL_DATE_FORMAT + ' h:mm:ss A'; }
        public static get ISO_STRING_WITHOUT_TIMEZONE(): string { return 'YYYY-MM-DDTHH:mm:ss'; }
        public static get TIMEZONE_FORMAT(): string { return 'Z'; }

        public static get FRIENDLY_FULL_DATE_FORMAT(): string { return 'MMMM D, YYYY'; }
        public static get FRIENDLY_MONTH_DAY_YEAR_FORMAT(): string { return 'MM/DD/YYYY'; }
        public static get FRIENDLY_MOBILE_MONTH_DAY_YEAR_FORMAT(): string { return 'M/D/YY'; }

        // Notifications
        public static get NOTIFICATION_SYSTEM_ID(): string { return '#system-notifications'; }

        // SearchablePagedOrganizationViewModel
        public static get PLAN_SEARCH_CRITERIA(): string { return 'PlanSearchCriteria'; }
        public static get ORGANIZATION_SORT_BY(): string { return 'OrganizationSortBy'; }
        public static get ORGANIZATION_NAME_FILTER(): string { return 'name'; }
    }
}