﻿using Arc4u.Configuration;
using Arc4u.Dependency;
using Arc4u.Diagnostics;
using Arc4u.IdentityModel.Claims;
using Arc4u.OAuth2.Token;
using Arc4u.Security.Principal;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using Arc4u.Dependency.Attribute;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Threading.Tasks;
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;
using Microsoft.Extensions.Logging;

namespace Arc4u.OAuth2.Security.Principal
{
    [Export(typeof(IClaimsFiller))]
    public class ClaimsProxy : IClaimsFiller
    {
        public ClaimsProxy(IContainerResolve container, IAppSettings appSettings, Config config, IHttpClientFactory httpClientFactory, ILogger<ClaimsProxy> logger)
        {
            _container = container;
            _httpClientFactory = httpClientFactory;
            _applicationName = config?.ApplicationName ?? "Unknow";
            // read information to call the backend service from configuration.
            _url = appSettings.Values.ContainsKey("arc4u_ClaimsProxyUri") ? appSettings.Values["arc4u_ClaimsProxyUri"] : null;
            _jsonSerializer = new DataContractJsonSerializer(typeof(IEnumerable<ClaimDto>));
            _logger = logger;
        }

        private string _url;
        private readonly DataContractJsonSerializer _jsonSerializer;
        private string _applicationName;
        protected readonly IContainerResolve _container;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ClaimsProxy> _logger;

        public async Task<IEnumerable<ClaimDto>> GetAsync(IIdentity identity, IEnumerable<IKeyValueSettings> settings, object parameter)
        {
            var result = new List<ClaimDto>();

            if (null == identity)
            {
                _logger.Technical().LogError($"A null identity was received. No Claims will be generated.");
                return result;
            }

            if (null == settings || settings.Count() == 0)
            {
                _logger.Technical().LogError($"We need token settings to call the backend.");
                return result;
            }
            if (!settings.Any(s => s.Values.ContainsKey(TokenKeys.AuthenticationTypeKey) && s.Values[TokenKeys.AuthenticationTypeKey].Equals(identity.AuthenticationType)))
            {
                _logger.Technical().LogDebug($"Skip fetching claims, no setting found for authentication type {identity.AuthenticationType}.");
                return result;
            }

            try
            {
                // Check before the url and application name is defined!
                if (String.IsNullOrWhiteSpace(_url))
                    // no override rule, use the standard endpoint defined.
                    _url = settings.First().Values[TokenKeys.RootServiceUrlKey].TrimEnd('/') + "/api/claims";
                else
                    _url = String.Format(_url, _applicationName);

                _logger.Technical().System($"Call back-end service for authorization, endpoint = {_url}.").Log();

                IPlatformParameters platformParameters = parameter is IPlatformParameters parameters ? parameters : null;

                // Check if we need to do something before calling the backend like force the start of a vpn.
                Network.Handler.OnCalling?.Invoke(new Uri(_url));

                // call the backend service!
                var client = _httpClientFactory.CreateClient("ClaimsProxy");

                var response = await client.GetAsync(_url);
                if (response.IsSuccessStatusCode)
                {
                    _logger.Technical().System($"Call service {_url} succeeds.").Log();
                    String responseString = await response.Content.ReadAsStringAsync();
                    // Add the claims.
                    result.AddRange(_jsonSerializer.ReadObject<IEnumerable<ClaimDto>>(responseString));
                    _logger.Technical().System($"{result.Count} claim(s) received.").Log();
                }
                else
                {
                    _logger.Technical().LogError($"Call service {_url} gives error status ${response.StatusCode}.");
                }

            }
            catch (Exception exception)
            {
                Exception inner = exception.InnerException;
                while (null != inner)
                {
                    _logger.Technical().LogException(inner);
                    inner = inner.InnerException;
                }

                _logger.Technical().LogException(exception);
            }

            return result;
        }
    }
}
