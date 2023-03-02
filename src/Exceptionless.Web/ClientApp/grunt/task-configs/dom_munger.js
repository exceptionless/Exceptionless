module.exports = {
    read: {
        options: {
            read: [
                {selector: 'script[data-concat!="false"]', attribute: 'src', writeto: 'appjs'},
                {selector: 'link[rel="stylesheet"][data-concat="true"]', attribute: 'href', writeto: 'appcss'}
            ]
        },
        src: 'index.html'
    },
    update: {
        options: {
            remove: ['script[data-remove!="false"]', 'link[data-remove="true"]'],
            append: [
                {
                    selector: 'body',
                    html: '<script src="/app.min.js"></script><script src="/app.config.js"></script>'
                },
                {selector: 'head', html: '<link rel="stylesheet" href="/app.min.css">'}
            ]
        },
        src: 'index.html',
        dest: 'dist/index.html'
    }
};