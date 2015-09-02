$(function () {
    $('#input_apiKey').off();
    $('#input_apiKey').on('change', function () {
        var key = this.value;
        if (key && key.trim() !== '') {
            key = 'Bearer ' + key;
            window.authorizations.add('key', new ApiKeyAuthorization('Authorization', key, 'header'));
        }
    });

    $(document).prop('title', 'Exceptionless API');
    $('#logo').text(' ');
    $('#logo').attr("href", "https://www.exceptionless.io/");
    $('#logo').show();
})();