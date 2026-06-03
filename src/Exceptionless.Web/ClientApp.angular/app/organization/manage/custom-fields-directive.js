(function () {
    "use strict";

    angular.module("app.organization").directive("customFields", function () {
        return {
            bindToController: true,
            restrict: "E",
            replace: true,
            scope: {
                organizationId: "=",
                hasPremiumFeatures: "=",
            },
            templateUrl: "app/organization/manage/custom-fields-directive.tpl.html",
            controller: function (
                $ExceptionlessClient,
                billingService,
                dialogService,
                notificationService,
                organizationService,
                translateService
            ) {
                var vm = this;

                var INDEX_TYPES = ["keyword", "boolean", "date", "double", "integer"];

                function getCustomFields() {
                    return organizationService.getCustomFields(vm.organizationId).then(
                        function (response) {
                            vm.fields = response.data.plain ? response.data.plain() : response.data;
                        },
                        function () {
                            notificationService.error(
                                translateService.T("An error occurred while loading custom fields.")
                            );
                        }
                    );
                }

                function addField() {
                    if (!vm.hasPremiumFeatures) {
                        return billingService
                            .confirmUpgradePlan(
                                translateService.T("Custom fields require a paid plan. Please upgrade to add custom fields."),
                                vm.organizationId
                            )
                            .catch(function () {});
                    }

                    if (!vm.newField.name || !vm.newField.indexType) {
                        return;
                    }

                    return organizationService
                        .addCustomField(vm.organizationId, {
                            name: vm.newField.name,
                            indexType: vm.newField.indexType,
                            description: vm.newField.description || undefined,
                        })
                        .then(
                            function () {
                                vm.newField = { name: "", indexType: "keyword", description: "" };
                                vm.showAddForm = false;
                                notificationService.success(translateService.T("Custom field created successfully."));
                                return getCustomFields();
                            },
                            function (response) {
                                if (response.status === 426) {
                                    return billingService
                                        .confirmUpgradePlan(
                                            (response.data && (response.data.detail || response.data.title)) || undefined,
                                            vm.organizationId
                                        )
                                        .catch(function () {});
                                }

                                var message = translateService.T("An error occurred while creating the custom field.");
                                if (response.data && (response.data.detail || response.data.title)) {
                                    message += " " + (response.data.detail || response.data.title);
                                }
                                notificationService.error(message);
                            }
                        );
                }

                function removeField(field) {
                    return dialogService
                        .confirmDanger(
                            translateService.T(
                                'Are you sure you want to delete the "' +
                                    field.name +
                                    '" custom field? This field will no longer be indexed for new events. Existing indexed data will remain searchable until those events expire per your retention policy.'
                            ),
                            translateService.T("Delete Custom Field")
                        )
                        .then(function () {
                            return organizationService.removeCustomField(vm.organizationId, field.id).then(
                                function () {
                                    notificationService.success(
                                        translateService.T("Custom field queued for deletion.")
                                    );
                                    return getCustomFields();
                                },
                                function (response) {
                                    var message = translateService.T("An error occurred while deleting the custom field.");
                                    if (response.data && (response.data.detail || response.data.title)) {
                                        message += " " + (response.data.detail || response.data.title);
                                    }
                                    notificationService.error(message);
                                }
                            );
                        })
                        .catch(function () {});
                }

                this.$onInit = function $onInit() {
                    vm.source = "exceptionless.organization.customFields";
                    vm.fields = [];
                    vm.indexTypes = INDEX_TYPES;
                    vm.newField = { name: "", indexType: "keyword", description: "" };
                    vm.showAddForm = false;
                    vm.addField = addField;
                    vm.removeField = removeField;
                    vm.getCustomFields = getCustomFields;

                    $ExceptionlessClient.submitFeatureUsage(vm.source);
                    getCustomFields();
                };
            },
            controllerAs: "vm",
        };
    });
})();
