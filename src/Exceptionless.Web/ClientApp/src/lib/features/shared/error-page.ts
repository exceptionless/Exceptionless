export function getErrorMessage(status: number, fallback?: string): string {
    if (status === 403) {
        return 'Your account is signed in, but it does not have permission to open this resource.';
    }

    if (status === 404) {
        return 'The page may have moved, the link may be stale, or the item may no longer exist.';
    }

    if (status === 410) {
        return 'The item may have been removed, archived, or replaced by a newer location.';
    }

    if (status === 426) {
        return 'The resource exists, but your current plan or account state cannot open it yet.';
    }

    if (status >= 500) {
        return 'The request reached Exceptionless, but the service could not finish it. Try again in a moment.';
    }

    return fallback ?? 'Something unexpected happened while opening this page.';
}

export function getErrorTitle(status: number): string {
    if (status === 403) {
        return "You don't have access to this page";
    }

    if (status === 404) {
        return "We couldn't find that page";
    }

    if (status === 410) {
        return 'That page is no longer available';
    }

    if (status === 426) {
        return 'This page needs a plan change';
    }

    if (status >= 500) {
        return 'Something went wrong on our side';
    }

    return 'This page hit an error';
}
