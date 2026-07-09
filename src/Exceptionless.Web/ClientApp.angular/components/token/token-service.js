(function () {
    "use strict";

    angular.module("exceptionless.token", ["restangular"]).factory("tokenService", function (Restangular) {
        var _updatableProperties = ["is_disabled", "notes"];

        function create(options) {
            var token = {
                organization_id: options.organization_id,
                project_id: options.project_id,
                scopes: ["client"],
            };

            return Restangular.all("tokens").post(token);
        }

        function getById(id) {
            return Restangular.one("tokens", id).get();
        }

        function getByOrganizationId(id, options) {
            return Restangular.one("organizations", id)
                .all("tokens")
                .getList(options || {});
        }

        function getByProjectId(id, options) {
            return Restangular.one("projects", id)
                .all("tokens")
                .getList(options || {});
        }

        function getProjectDefault(id) {
            return Restangular.one("projects", id).one("tokens", "default").get();
        }

        function remove(id) {
            return Restangular.one("tokens", id).remove();
        }

        function update(id, token) {
            return Restangular.one("tokens", id).customPATCH(
                toJsonPatch(token, _updatableProperties),
                "",
                {},
                { "Content-Type": "application/json-patch+json" }
            );
        }

        function toJsonPatch(obj, properties) {
            return properties
                .filter(function (key) {
                    return obj[key] !== undefined;
                })
                .map(function (key) {
                    return { op: "replace", path: "/" + key, value: obj[key] };
                });
        }

        var service = {
            create: create,
            getById: getById,
            getByOrganizationId: getByOrganizationId,
            getByProjectId: getByProjectId,
            getProjectDefault: getProjectDefault,
            remove: remove,
            update: update,
        };
        return service;
    });
})();
