(function () {
    "use strict";

    angular.module("exceptionless.system-notification", ["exceptionless.refresh"]).directive("systemNotification", [
        function () {
            return {
                restrict: "E",
                templateUrl: "components/system-notification/system-notification-directive.tpl.html",
                controller: ["$http", "$scope", "$window", "BASE_URL", "SYSTEM_NOTIFICATION_MESSAGE", function ($http, $scope, $window, BASE_URL, SYSTEM_NOTIFICATION_MESSAGE) {
                    var svm = this;
                    var dismissedSystemNotificationStorageKey = "exceptionless.system-notification.dismissed-key";

                    function getSystemNotificationClass(level) {
                        switch (level) {
                            case "Error":
                                return "alert-danger";
                            case "Warning":
                                return "alert-warning";
                            default:
                                return "alert-info";
                        }
                    }

                    function normalizeSystemNotificationTarget(target) {
                        var normalizedTarget = (target || "Both").replace(/[^a-z]/gi, "").toLowerCase();

                        if (normalizedTarget === "legacy" || normalizedTarget === "legacyui" || normalizedTarget === "oldui" || normalizedTarget === "old" || normalizedTarget === "angular") {
                            return "Legacy";
                        }

                        if (normalizedTarget === "modern" || normalizedTarget === "modernui" || normalizedTarget === "newui" || normalizedTarget === "new" || normalizedTarget === "svelte") {
                            return "Modern";
                        }

                        return "Both";
                    }

                    function getDismissedSystemNotificationKey() {
                        try {
                            return $window.localStorage.getItem(dismissedSystemNotificationStorageKey);
                        } catch (e) {
                            return null;
                        }
                    }

                    function getSystemNotificationDateKey(date) {
                        if (!date) {
                            return null;
                        }

                        var parsedDate = new Date(date);
                        return isNaN(parsedDate.getTime()) ? date : parsedDate.toISOString();
                    }

                    function getSystemNotificationKey(notification) {
                        if (!notification) {
                            return null;
                        }

                        return JSON.stringify([getSystemNotificationDateKey(notification.date), notification.level || "Info", normalizeSystemNotificationTarget(notification.target), notification.message || SYSTEM_NOTIFICATION_MESSAGE || ""]);
                    }

                    function processSystemNotification(notification) {
                        if (notification) {
                            var target = normalizeSystemNotificationTarget(notification.target);
                            if (target === "Modern") {
                                svm.systemNotificationMessage = null;
                                return;
                            }
                            svm.systemNotificationKey = getSystemNotificationKey(notification);
                            svm.systemNotificationMessage = notification.message || SYSTEM_NOTIFICATION_MESSAGE;
                            svm.systemNotificationClass = getSystemNotificationClass(notification.level);
                        }
                    }

                    function dismissSystemNotification() {
                        svm.dismissedSystemNotificationKey = svm.systemNotificationKey;
                        try {
                            $window.localStorage.setItem(dismissedSystemNotificationStorageKey, svm.systemNotificationKey);
                        } catch (e) {
                            // Ignore storage failures; dismissal still works for the current page lifetime.
                        }
                    }

                    function showFallbackSystemNotification() {
                        svm.systemNotificationMessage = SYSTEM_NOTIFICATION_MESSAGE;
                        svm.systemNotificationClass = "alert-info";
                        svm.systemNotificationKey = SYSTEM_NOTIFICATION_MESSAGE ? JSON.stringify([null, "Info", "Both", SYSTEM_NOTIFICATION_MESSAGE]) : null;
                    }

                    function refreshSystemNotification() {
                        return $http.get(BASE_URL + "/api/v2/notifications/system").then(function (response) {
                            if (response.data) {
                                processSystemNotification(response.data);
                            } else {
                                showFallbackSystemNotification();
                            }
                        }).catch(angular.noop);
                    }

                    this.$onInit = function $onInit() {
                        svm.dismissSystemNotification = dismissSystemNotification;
                        svm.processSystemNotification = processSystemNotification;
                        svm.dismissedSystemNotificationKey = getDismissedSystemNotificationKey();
                        showFallbackSystemNotification();

                        refreshSystemNotification();

                        $window.addEventListener("focus", refreshSystemNotification);
                        $window.addEventListener("online", refreshSystemNotification);
                        $scope.$on("$destroy", function () {
                            $window.removeEventListener("focus", refreshSystemNotification);
                            $window.removeEventListener("online", refreshSystemNotification);
                        });
                    };
                }],
                controllerAs: "svm",
            };
        },
    ]);
})();
