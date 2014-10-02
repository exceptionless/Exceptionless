/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class Project {
        id: string;
        name: string;
        organizationId: string;
        timeZone: string;
        apiKeys: string[];
        configuration = ko.observableArray<models.KeyValuePair>([]);
        promotedTabs: string[];

        constructor(id: string, organizationId: string, name: string, timeZone?: string, apiKeys?: string[], configuration?: any, promotedTabs?: any) {
            this.id = id;
            this.name = name;
            this.organizationId = organizationId;
            this.timeZone = timeZone;

            this.apiKeys = apiKeys ? apiKeys : [];
            this.promotedTabs = promotedTabs ? promotedTabs : [];

            if (configuration && configuration.Settings) {
                var settings: models.KeyValuePair[] = [];
                for (var s in configuration.Settings) {
                    settings.push(new models.KeyValuePair(s, configuration.Settings[s]));
                }

                this.configuration(settings);
            }

            ko.track(this);
        }
    }
}