(function () {
    "use strict";

    angular
        .module("exceptionless.stack", ["restangular", "exceptionless.filter"])
        .factory("stackService", function (filterService, Restangular) {
            function addLink(id, url) {
                return Restangular.one("stacks", id)
                    .one("add-link")
                    .customPOST({ value: url }, undefined, undefined, {});
            }

            function changeStatus(id, status) {
                return Restangular.one("stacks", id).post("change-status", null, { status: status });
            }

            function getAll(options) {
                var mergedOptions = filterService.apply(options);
                var organization = filterService.getOrganizationId();
                if (organization) {
                    return Restangular.one("organizations", organization).all("stacks").getList(mergedOptions);
                }

                var project = filterService.getProjectId();
                if (project) {
                    return Restangular.one("projects", project).all("stacks").getList(mergedOptions);
                }

                return Restangular.all("stacks").getList(mergedOptions);
            }

            function getById(id) {
                return Restangular.one("stacks", id).get();
            }

            function getRollups(options, optionsCallback) {
                optionsCallback = angular.isFunction(optionsCallback)
                    ? optionsCallback
                    : function (o) {
                          return o;
                      };
                var mergedOptions = optionsCallback(filterService.apply(options));
                mergedOptions.mode = "stack";
                var organization = filterService.getOrganizationId();
                if (organization) {
                    return Restangular.one("organizations", organization).all("events").getList(mergedOptions);
                }

                var project = filterService.getProjectId();
                if (project) {
                    return Restangular.one("projects", project).all("events").getList(mergedOptions);
                }

                return Restangular.all("events").getList(mergedOptions);
            }

            function markCritical(id) {
                return Restangular.one("stacks", id).one("mark-critical").post();
            }

            function markNotCritical(id) {
                return Restangular.one("stacks", id).one("mark-critical").remove();
            }

            function markFixed(id, version) {
                return Restangular.one("stacks", id).post("mark-fixed", null, { version: version });
            }

            function markSnoozed(id, snoozeUntilUtc) {
                return Restangular.one("stacks", id).post("mark-snoozed", null, { snoozeUntilUtc: snoozeUntilUtc });
            }

            function promote(id) {
                return Restangular.one("stacks", id).one("promote").post();
            }

            function remove(id) {
                return Restangular.one("stacks", id).remove();
            }

            function removeLink(id, url) {
                return Restangular.one("stacks", id)
                    .one("remove-link")
                    .customPOST({ value: url }, undefined, undefined, {});
            }

            var service = {
                addLink: addLink,
                changeStatus: changeStatus,
                getAll: getAll,
                getById: getById,
                getRollups: getRollups,
                markCritical: markCritical,
                markNotCritical: markNotCritical,
                markFixed: markFixed,
                markSnoozed: markSnoozed,
                promote: promote,
                remove: remove,
                removeLink: removeLink,
            };

            return service;
        });
})();
