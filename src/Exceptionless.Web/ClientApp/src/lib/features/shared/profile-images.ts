export const PROFILE_IMAGE_MAX_FILE_SIZE = 5 * 1024 * 1024;
export const PROFILE_IMAGE_MAX_FILE_SIZE_LABEL = '5 MB';

export function getProfileImageFileError(file: File): null | string {
    if (file.size > PROFILE_IMAGE_MAX_FILE_SIZE) {
        return `Image files must be ${PROFILE_IMAGE_MAX_FILE_SIZE_LABEL} or smaller.`;
    }

    return null;
}
