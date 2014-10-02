/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class ProjectHook {
        id: string;
        projectId: string;
        url: string;
        eventTypes: string[];

        constructor(id: string, projectId: string, url: string, eventTypes?: string[]) {
            this.id = id;
            this.projectId = projectId;
            this.url = url;

            this.eventTypes = eventTypes ? eventTypes : [];

            ko.track(this);
        }
    }
}