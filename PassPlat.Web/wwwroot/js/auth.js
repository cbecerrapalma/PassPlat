window.AuthInterop = {
    parseFragment: function () {
        var hash = window.location.hash;
        if (!hash || hash.length < 2) return null;
        var params = new URLSearchParams(hash.substring(1));
        return {
            accessToken: params.get('accessToken') || '',
            refreshToken: params.get('refreshToken') || '',
            idUsuario: params.get('idUsuario') || '',
            idTenant: params.get('idTenant') || '',
            nomUsuario: params.get('nomUsuario') || '',
            reqCambioPwd: params.get('reqCambioPwd') || 'false'
        };
    },
    clearFragment: function () {
        window.location.hash = '';
    }
};
