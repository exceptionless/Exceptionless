/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export interface ProjectData {
        id: string;
        organization: models.Organization;
        totalErrorCount: number;
    }

    export class ProjectInfo {
        id: string;
        name: string;
        organizationId: string;
        timeZoneOffset: number;
        stackCount: number;
        errorCount: number;
        totalErrorCount: number;

        constructor(id: string, name: string, organizationId: string, timeZoneOffset: number, stackCount: number, errorCount: number, totalErrorCount: number) {
            this.id = id;
            this.name = name;
            this.organizationId = organizationId;
            this.timeZoneOffset = timeZoneOffset;

            this.stackCount = stackCount;
            this.errorCount = errorCount;
            this.totalErrorCount = totalErrorCount;

            ko.track(this);
        }

        public get organization(): models.Organization {
            return ko.utils.arrayFirst(App.organizations(), (organization: models.Organization) => { return organization.id === this.organizationId; });
        }
    }
}