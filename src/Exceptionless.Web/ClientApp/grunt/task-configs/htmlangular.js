module.exports = {
    options: {
        customtags: [
            'accordion',
            'date-*',
            'events',
            'extended-data-item',
            'intercom',
            'object-dump',
            'organization-notifications',
            'progressbar',
            'project-filter',
            'projects',
            'rate-limit',
            'rickshaw',
            'search-filter',
            'simple-stack-trace',
            'stacks',
            'stack-trace',
            'summary',
            'timeago',
            'toaster-container'
        ],
        customattrs: [
            'auto-active',
            'autocapitalize',
            'autocorrect',
            'autoscroll',
            'checklist-*',
            'clip-*',
            'dropdown',
            'dropdown-*',
            'email-address-available-validator',
            'gravatar-*',
            'is-*',
            'lines',
            'match',
            'organization-name-available-validator',
            'payments-*',
            'project-name-available-validator',
            'refresh-*',
            'search-filter-validator',
            'typeahead',
            'truncate',
            'ui-*',
            'x-autocompletetype'
        ],
        tmplext: 'tpl.html',
        reportpath: null,
        relaxerror: [
            'A table row was 8 columns wide and exceeded the column count established by the first row (5).',
            'Attribute href without an explicit value seen. The attribute may be dropped by IE7.',
            'Element img is missing required attribute src.',
            'Element tabset not allowed as child of element div in this context.',
            'Element div not allowed as child of element pre in this context.',
            'Table columns in range 7…8 established by element td have no cells beginning in them.'
        ]
    },
    files: {
        src: ['index.html', 'app/**/*.html', 'components/**/*.html']
    }
};