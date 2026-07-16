export interface CustomFieldDefinition {
    createdUtc: string;
    description?: string;
    displayOrder: number;
    id: string;
    indexType: string;
    name: string;
    updatedUtc: string;
}

export interface NewCustomFieldDefinition {
    description?: string;
    displayOrder?: number;
    indexType: string;
    name: string;
}

export interface UpdateCustomFieldDefinition {
    description?: string;
    displayOrder?: number;
}

export const INDEX_TYPES = ['keyword', 'string', 'int', 'long', 'float', 'double', 'bool', 'date'] as const;
export type IndexType = (typeof INDEX_TYPES)[number];

export const INDEX_TYPE_LABELS: Record<IndexType, string> = {
    bool: 'Boolean',
    date: 'Date/Time',
    double: 'Double',
    float: 'Float',
    int: 'Integer',
    keyword: 'Keyword',
    long: 'Long',
    string: 'Text'
};

export const INDEX_TYPE_DESCRIPTIONS: Record<IndexType, string> = {
    bool: 'True/false values.',
    date: 'ISO 8601 date or timestamp.',
    double: '64-bit decimal. Higher precision.',
    float: '32-bit decimal. Lower precision.',
    int: '32-bit whole number (-2B to 2B).',
    keyword: 'Exact-match string. Best for IDs, codes, tags.',
    long: '64-bit whole number. For very large integers.',
    string: 'Full-text search. Best for messages and descriptions.'
};
