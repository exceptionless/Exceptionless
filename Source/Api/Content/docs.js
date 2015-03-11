$('#input_apiKey').change(function() {
    var key = $('#input_apiKey')[0].value;
    if (key && key.trim() !== '') {
        key = 'Bearer ' + key;
        window.authorizations.add('key', new ApiKeyAuthorization('Authorization', key, 'header'));
    }
});