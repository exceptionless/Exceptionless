// By: Hans Fjällemark and John Papa
// https://github.com/CodeSeven/KoLite

(function (factory) {
    if (typeof require === "function" && typeof exports === "object" && typeof module === "object") {
        factory(require("knockout"), exports);
    } else if (typeof define === "function" && define["amd"]) {
        define(["knockout", "exports"], factory);
    } else {
        factory(ko, ko);
    }
}(function (ko, exports) {
    if (typeof (ko) === undefined) {
        throw 'Knockout is required, please ensure it is loaded before loading the commanding plug-in';
    }

    function wrapAccessor(accessor) {
        return function () {
            return accessor;
        };
    };

    exports.command = function(options) {
        var
            self = function() {
                return self.execute.apply(this, arguments);
            },
            canExecuteDelegate = options.canExecute,
            executeDelegate = options.execute;

        self.canExecute = ko.computed(function() {
            return canExecuteDelegate ? canExecuteDelegate() : true;
        });

        self.execute = function (arg1, arg2) {
             // Needed for anchors since they don't support the disabled state
            if (!self.canExecute()) return

            return executeDelegate.apply(this, [arg1, arg2]);
        };

        return self;
    };

    exports.asyncCommand = function(options) {
        var
            self = function() {
                return self.execute.apply(this, arguments);
            },
            canExecuteDelegate = options.canExecute,
            executeDelegate = options.execute,

            completeCallback = function() {
                self.isExecuting(false);
            };

        self.isExecuting = ko.observable();

        self.canExecute = ko.computed(function() {
            return canExecuteDelegate ? canExecuteDelegate(self.isExecuting()) : !self.isExecuting();
        });

        self.execute = function (arg1, arg2) {
             // Needed for anchors since they don't support the disabled state
            if (!self.canExecute()) return

            var args = []; // Allow for these arguments to be passed on to execute delegate

            if (executeDelegate.length >= 2) {
                args.push(arg1);
            }

            if (executeDelegate.length >= 3) {
                args.push(arg2);
            }

            args.push(completeCallback);
            self.isExecuting(true);
            return executeDelegate.apply(this, args);
        };

        return self;
    };

    ko.bindingHandlers.command = {
        init: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
            var
                value = valueAccessor(),
                commands = value.execute ? { click: value } : value,

                isBindingHandler = function (handler) {
                    return ko.bindingHandlers[handler] !== undefined;
                },

                initBindingHandlers = function () {
                    for (var command in commands) {
                        if (!isBindingHandler(command)) {
                            continue;
                        };

                        ko.bindingHandlers[command].init(
                            element,
                            wrapAccessor(commands[command].execute),
                            allBindingsAccessor,
                            viewModel,
                            bindingContext
                        );
                    }
                },

                initEventHandlers = function () {
                    var events = {};

                    for (var command in commands) {
                        if (!isBindingHandler(command)) {
                            events[command] = commands[command].execute;
                        }
                    }

                    ko.bindingHandlers.event.init(
                        element,
                        wrapAccessor(events),
                        allBindingsAccessor,
                        viewModel,
                        bindingContext);
                };

            initBindingHandlers();
            initEventHandlers();
        },

        update: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
            var commands = valueAccessor();
            var canExecute = commands.canExecute;

            if (!canExecute) {
                for (var command in commands) {
                    if (commands[command].canExecute) {
                        canExecute = commands[command].canExecute;
                        break;
                    }
                }
            }

            if (!canExecute) {
                return;
            }

            ko.bindingHandlers.enable.update(element, canExecute, allBindingsAccessor, viewModel, bindingContext);
        }
    };
}));
