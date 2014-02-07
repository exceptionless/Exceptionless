/// <reference path="../exceptionless.ts" />

module exceptionless.account {
    export class ManageAccountViewModel extends ViewModelBase {
        private _navigationViewModel: NavigationViewModel;
        emailNotificationsEnabled = ko.observable<boolean>(false);
        isVerified = ko.observable<boolean>(false);

        updatePasswordCommand: KoliteCommand;
        saveCommand: KoliteCommand;

        constructor(elementId: string, navigationElementId: string, tabElementId: string, nameEmailFormSelector: string, passwordFormSelector: string, emailNotificationsEnabled: boolean, isVerified: boolean) {
            super(elementId);

            this._navigationViewModel = new NavigationViewModel(navigationElementId);
            TabUtil.init(tabElementId);
            
            this.updatePasswordCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    return !isExecuting;
                },
                execute: (complete) => {
                    if (!$(passwordFormSelector).valid()) {
                        complete();
                        return;
                    }

                    DataUtil.submitForm($(passwordFormSelector),
                        (data) => {
                            App.showSuccessNotification('Your password has been successfully updated!');
                            complete();

                            $(passwordFormSelector + ' input').each((index:number, element: any) => element.value = null);
                        }, (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                            if (jqXHR.status != 400)
                                App.showErrorNotification('An error occurred while updating your password.');

                            complete();
                        });
                }
            });

            this.saveCommand = ko.asyncCommand({
                canExecute: (isExecuting) => {
                    return !isExecuting;
                },
                execute: (complete) => {
                    if (!$(nameEmailFormSelector).valid()) {
                        complete();
                        return;
                    }

                    DataUtil.submitForm($(nameEmailFormSelector),
                        (data) => {
                            this.isVerified(data.IsVerified);

                            App.showSuccessNotification('Saved!');
                            complete();
                        }, (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                            if (jqXHR.status != 400)
                                App.showErrorNotification('An error occurred while saving your changes.');

                            complete();
                        });
                }
            });
            
            this.isVerified(isVerified);

            this.emailNotificationsEnabled.subscribe((value: boolean) => $('#EmailNotificationsEnabled').val(value.toString()));
            this.emailNotificationsEnabled(emailNotificationsEnabled);

            this.applyBindings();
        }

        public resendVerificationEmail(): void {
            $.ajax('/account/resend-verification-email', {
                dataType: 'json',
                success: () => App.showSuccessNotification('Your verification email has been successfully sent!'),
                error: () => App.showErrorNotification('An error occurred while resending the verification email.')
            });
        }
    }
}