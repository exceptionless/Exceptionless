import type { Primitive, Schema } from './types.js';

import { isPrimitive } from './utils.js';

export const traverseSchema = ({
    cb,
    follower = null,
    path = '',
    schema
}: {
    cb: (p: { follower?: any | null; path: string; primitive: Primitive }) => void;
    follower?: any;
    path?: string;
    schema: Schema;
}) => {
    for (const [key, schemaType] of Object.entries(schema)) {
        const isArray = Array.isArray(schemaType);
        const type = isArray ? schemaType[0] : schemaType;
        const primitive = isPrimitive(type) ? type : undefined;
        const schema = isPrimitive(type) ? undefined : type;
        const newPath = path ? `${path}.${key}` : key;
        if (primitive) {
            if (isArray) {
                for (let i = 0; ; i++) {
                    const arrayPath = `${newPath}.${i}`;
                    const value = follower[key]?.[i];
                    if (!value) {
                        break;
                    }

                    cb({ follower: value, path: arrayPath, primitive });
                }
            } else {
                cb({ follower: follower[key], path: newPath, primitive });
            }
        } else if (schema) {
            if (isArray) {
                for (let i = 0; ; i++) {
                    const arrayPath = `${newPath}.${i}`;
                    const value = follower[key]?.[i];
                    if (!value) {
                        break;
                    }

                    traverseSchema({
                        cb,
                        follower: value,
                        path: arrayPath,
                        schema
                    });
                }
            } else {
                traverseSchema({
                    cb,
                    follower: follower[key],
                    path: newPath,
                    schema
                });
            }
        }
    }
};
