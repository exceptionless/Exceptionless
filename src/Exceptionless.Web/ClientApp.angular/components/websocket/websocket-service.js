(function () {
    "use strict";

    angular
        .module("exceptionless.websocket", ["app.config", "exceptionless", "exceptionless.auth"])
        .factory("websocketService", function ($ExceptionlessClient, $rootScope, $timeout, authService, BASE_URL) {
            var source = "exceptionless.websocket.websocketService";
            var _abortController;
            var _reconnectTimeout;
            var _reconnectAttempts = 0;
            var _forcedClose = false;

            function start() {
                startDelayed(1);
            }

            function startDelayed(delay) {
                if (_abortController || _reconnectTimeout) {
                    stop();
                }

                _reconnectTimeout = $timeout(function () {
                    _reconnectTimeout = null;
                    connect();
                }, delay || 1000);
            }

            function connect() {
                _forcedClose = false;
                _abortController = new AbortController();
                var signal = _abortController.signal;

                var url = BASE_URL + "/api/v2/push";
                var token = authService.getToken();

                fetch(url, {
                    headers: {
                        Accept: "text/event-stream",
                        Authorization: "Bearer " + token,
                    },
                    signal: signal,
                })
                    .then(function (response) {
                        if (!response.ok) {
                            if (response.status === 401 || response.status === 403) {
                                // Auth failure - don't reconnect
                                return;
                            }
                            throw new Error("SSE connection failed: " + response.status);
                        }

                        _reconnectAttempts = 0;
                        var reader = response.body.getReader();
                        var decoder = new TextDecoder();
                        var buffer = "";

                        function readChunk() {
                            return reader.read().then(function (result) {
                                if (result.done) {
                                    if (!_forcedClose) {
                                        scheduleReconnect();
                                    }
                                    return;
                                }

                                buffer += decoder.decode(result.value, { stream: true });

                                var messages = buffer.split("\n\n");
                                buffer = messages.pop() || "";

                                messages.forEach(function (message) {
                                    if (!message.trim()) return;

                                    var lines = message.split("\n");
                                    var data = "";

                                    lines.forEach(function (line) {
                                        if (line.indexOf("data: ") === 0) {
                                            data += line.slice(6);
                                        } else if (line.indexOf("data:") === 0) {
                                            data += line.slice(5);
                                        }
                                        // Comments (: keepalive) are ignored
                                    });

                                    if (data) {
                                        var parsed = JSON.parse(data);
                                        if (!parsed || !parsed.type) {
                                            return;
                                        }

                                        if (parsed.message && parsed.message.change_type >= 0) {
                                            parsed.message.added = parsed.message.change_type === 0;
                                            parsed.message.updated = parsed.message.change_type === 1;
                                            parsed.message.deleted = parsed.message.change_type === 2;
                                        }

                                        $rootScope.$emit(parsed.type, parsed.message);

                                        // This event is fired when a user is added or removed from an organization.
                                        if (parsed.type === "UserMembershipChanged" && parsed.message && parsed.message.organization_id) {
                                            $rootScope.$emit("OrganizationChanged", parsed.message);
                                            $rootScope.$emit("ProjectChanged", parsed.message);
                                        }
                                    }
                                });

                                return readChunk();
                            });
                        }

                        return readChunk();
                    })
                    .catch(function (error) {
                        if (signal.aborted && _forcedClose) {
                            return;
                        }

                        if (!_forcedClose) {
                            scheduleReconnect();
                        }
                    });
            }

            function scheduleReconnect() {
                var multiplier = 1;

                _reconnectAttempts++;
                for (var attempt = 1; attempt < _reconnectAttempts; attempt++) {
                    multiplier *= 2;
                }

                var delay = Math.min(1000 * multiplier, 30000);
                _reconnectTimeout = $timeout(function () {
                    _reconnectTimeout = null;
                    connect();
                }, delay);
            }

            function stop() {
                if (_reconnectTimeout) {
                    $timeout.cancel(_reconnectTimeout);
                    _reconnectTimeout = null;
                }

                if (_abortController) {
                    _forcedClose = true;
                    _abortController.abort();
                    _abortController = null;
                }
            }

            var service = {
                start: start,
                startDelayed: startDelayed,
                stop: stop,
            };

            return service;
        });
})();
