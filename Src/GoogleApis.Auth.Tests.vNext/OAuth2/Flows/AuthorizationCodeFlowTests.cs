/*
Copyright 2013 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xunit;
using LightMock;

using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Http;
using Google.Apis.Json;
using Google.Apis.Testing;
using Google.Apis.Tests;
using Google.Apis.Util;
using Google.Apis.Util.Store;

namespace Google.Apis.Auth.OAuth2.Flows
{
    /// <summary>Tests for <see cref="Google.Apis.Auth.OAuth2.AuthorizationCodeFlow"/>.</summary>
    public class AuthorizationCodeFlowTests
    {
        private const string TokenUrl = "https://token.com";
        private const string AuthorizationCodeUrl = "https://authorization.com";

        #region Constructor

        [Fact]
        public void TestConstructor_ArgumentException()
        {
            // ClientSecrets are missing.
            try
            {
                new AuthorizationCodeFlow(new AuthorizationCodeFlow.Initializer(
                    "https://authorization_code.com", "https://token.com"));
                Assert.True(false);
            }
            catch (ArgumentException ex)
            {
                Assert.True(ex.Message.Contains("You MUST set ClientSecret or ClientSecretStream"));
            }
        }

        [Fact]
        public void TestConstructor_DefaultValues()
        {
            var flow = CreateFlow();
            Assert.NotNull(flow.AccessMethod);
            Assert.IsType<BearerToken.AuthorizationHeaderAccessMethod>(flow.AccessMethod);
            Assert.Equal(flow.AuthorizationServerUrl, Is.EqualTo("https://authorization.com"));
            Assert.NotNull(flow.ClientSecrets);
            Assert.Equal(flow.ClientSecrets.ClientId, Is.EqualTo("id"));
            Assert.Equal(flow.ClientSecrets.ClientSecret, Is.EqualTo("secret"));
            Assert.IsType<SystemClock>(flow.Clock);
            Assert.Null(flow.DataStore);
            Assert.NotNull(flow.HttpClient);
            Assert.NotNull(flow.Scopes);
            Assert.Equal(flow.TokenServerUrl, Is.EqualTo("https://token.com"));

            Assert.Equal(flow.HttpClient.MessageHandler.UnsuccessfulResponseHandlers.Count(), Is.EqualTo(1));
            Assert.IsType<BackOffHandler>(flow.HttpClient.MessageHandler.UnsuccessfulResponseHandlers.First());
        }

        #endregion

        #region LoadToken

        [Fact]
        public void LoadTokenAsync_NoDataStore()
        {
            var flow = CreateFlow();
            Assert.Null(flow.LoadTokenAsync("user", CancellationToken.None).Result);
        }

        [Fact]
        public void LoadTokenAsync_NullResponse()
        {
            TaskCompletionSource<TokenResponse> tcs = new TaskCompletionSource<TokenResponse>();
            tcs.SetResult(null);
            Assert.Null(SubtestLoadTokenAsync(tcs));
        }

        [Fact]
        public void LoadTokenAsync_TokenResponse()
        {
            TokenResponse response = new TokenResponse
            {
                AccessToken = "access"
            };

            TaskCompletionSource<TokenResponse> tcs = new TaskCompletionSource<TokenResponse>();
            tcs.SetResult(response);
            var result = SubtestLoadTokenAsync(tcs);
            Assert.Equal(result, Is.EqualTo(response));
        }

        // TODO(neil-119): Moq hasn't been ported to CoreCLR yet, so using LightMock instead
        public class DataStoreMock : IDataStore
        {
            private readonly IInvocationContext<IDataStore> context;

            public DataStoreMock(IInvocationContext<IDataStore> context)
            {
                this.context = context;
            }

            public Task StoreAsync<T>(string key, T value)
            {
                return context.Invoke(f => f.StoreAsync<T>(key, value));
            }

            public Task DeleteAsync<T>(string key)
            {
                throw new NotImplementedException();
            }

            public Task<T> GetAsync<T>(string key)
            {
                return context.Invoke(f => f.GetAsync<T>(key));
            }

            public Task ClearAsync()
            {
                throw new NotImplementedException();
            }
        }

        private TokenResponse SubtestLoadTokenAsync(TaskCompletionSource<TokenResponse> tcs)
        {
            var mockContext = new MockContext<IDataStore>();
            var dataStoreMock = new DataStoreMock(mockContext);
            mockContext.Arrange(f => f.GetAsync<TokenResponse>("user")).Returns(tcs.Task);
            var flow = CreateFlow(dataStore: dataStoreMock);
            var result = flow.LoadTokenAsync("user", CancellationToken.None).Result;
            mockContext.Assert(f => f.GetAsync<TokenResponse>("user"));

            return result;
        }

        #endregion

        #region CreateAuthorizationCodeRequest

        [Fact]
        public void TestCreateAuthorizationCodeRequest()
        {
            var request = CreateFlow(scopes: new[] { "a", "b" }).CreateAuthorizationCodeRequest("redirect");
            Assert.Equal(request.AuthorizationServerUrl, Is.EqualTo(new Uri(AuthorizationCodeUrl)));
            Assert.Equal(request.ClientId, Is.EqualTo("id"));
            Assert.Equal(request.RedirectUri, Is.EqualTo("redirect"));
            Assert.Equal(request.ResponseType, Is.EqualTo("code"));
            Assert.Equal(request.Scope, Is.EqualTo("a b"));
            Assert.Null(request.State);
        }

        #endregion

        [Fact]
        public void TestExchangeCodeForTokenAsync()
        {
            
            var handler = new FetchTokenMessageHandler();
            handler.AuthorizationCodeTokenRequest = new AuthorizationCodeTokenRequest()
            {
                Code = "c0de",
                RedirectUri = "redIrect",
                Scope = "a"
            };
            MockHttpClientFactory mockFactory = new MockHttpClientFactory(handler);

            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);

            var mockContext = new MockContext<IDataStore>();
            var dataStoreMock = new DataStoreMock(mockContext);
            mockContext.Arrange(f => f.StoreAsync("uSer", The<TokenResponse>.IsAnyValue)).Returns(tcs.Task);

            var flow = CreateFlow(httpClientFactory: mockFactory, scopes: new[] { "a" }, dataStore: dataStoreMock);
            var response = flow.ExchangeCodeForTokenAsync("uSer", "c0de", "redIrect", CancellationToken.None).Result;
            SubtestTokenResponse(response);

            mockContext.Assert(f => f.StoreAsync("uSer", The<TokenResponse>.IsAnyValue));
        }

        [Fact]
        public void TestRefreshTokenAsync()
        {
            var handler = new FetchTokenMessageHandler();
            handler.RefreshTokenRequest = new RefreshTokenRequest()
            {
                RefreshToken = "REFRESH",
                Scope = "a"
            };
            MockHttpClientFactory mockFactory = new MockHttpClientFactory(handler);

            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);

            var mockContext = new MockContext<IDataStore>();
            var dataStoreMock = new DataStoreMock(mockContext);
            mockContext.Arrange(f => f.StoreAsync("uSer", The<TokenResponse>.IsAnyValue)).Returns(tcs.Task);

            var flow = CreateFlow(httpClientFactory: mockFactory, scopes: new[] { "a" }, dataStore: dataStoreMock);
            var response = flow.RefreshTokenAsync("uSer", "REFRESH", CancellationToken.None).Result;
            SubtestTokenResponse(response);

            mockContext.Assert(f => f.StoreAsync("uSer", The<TokenResponse>.IsAnyValue));
        }

        #region FetchToken

        /// <summary>
        /// Fetch token message handler, which expects an authorization code token request or a refresh token request.
        /// It verifies all the query parameters are valid and return an error response in case <see cref="Error"/> 
        /// is <c>true</c>.
        /// </summary>
        public class FetchTokenMessageHandler : CountableMessageHandler
        {
            internal AuthorizationCodeTokenRequest AuthorizationCodeTokenRequest { get; set; }
            internal RefreshTokenRequest RefreshTokenRequest { get; set; }
            internal bool Error { get; set; }

            protected override async Task<HttpResponseMessage> SendAsyncCore(HttpRequestMessage request,
                CancellationToken taskCancellationToken)
            {
                Assert.Equal(request.RequestUri, Is.EqualTo(new Uri(TokenUrl)));

                if (AuthorizationCodeTokenRequest != null)
                {
                    // Verify right parameters.
                    var content = await request.Content.ReadAsStringAsync();
                    foreach (var parameter in content.Split('&'))
                    {
                        var keyValue = parameter.Split('=');
                        switch (keyValue[0])
                        {
                            case "code":
                                Assert.Equal(keyValue[1], Is.EqualTo("c0de"));
                                break;
                            case "redirect_uri":
                                Assert.Equal(keyValue[1], Is.EqualTo("redIrect"));
                                break;
                            case "scope":
                                Assert.Equal(keyValue[1], Is.EqualTo("a"));
                                break;
                            case "grant_type":
                                Assert.Equal(keyValue[1], Is.EqualTo("authorization_code"));
                                break;
                            case "client_id":
                                Assert.Equal(keyValue[1], Is.EqualTo("id"));
                                break;
                            case "client_secret":
                                Assert.Equal(keyValue[1], Is.EqualTo("secret"));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException("Invalid parameter!");
                        }
                    }
                }
                else
                {
                    // Verify right parameters.
                    var content = await request.Content.ReadAsStringAsync();
                    foreach (var parameter in content.Split('&'))
                    {
                        var keyValue = parameter.Split('=');
                        switch (keyValue[0])
                        {
                            case "refresh_token":
                                Assert.Equal(keyValue[1], Is.EqualTo("REFRESH"));
                                break;
                            case "scope":
                                Assert.Equal(keyValue[1], Is.EqualTo("a"));
                                break;
                            case "grant_type":
                                Assert.Equal(keyValue[1], Is.EqualTo("refresh_token"));
                                break;
                            case "client_id":
                                Assert.Equal(keyValue[1], Is.EqualTo("id"));
                                break;
                            case "client_secret":
                                Assert.Equal(keyValue[1], Is.EqualTo("secret"));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException("Invalid parameter!");
                        }
                    }
                }

                var response = new HttpResponseMessage();
                if (Error)
                {
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    var serializedObject = NewtonsoftJsonSerializer.Instance.Serialize(new TokenErrorResponse
                    {
                        Error = "error",
                        ErrorDescription = "desc",
                        ErrorUri = "uri"
                    });
                    response.Content = new StringContent(serializedObject, Encoding.UTF8);
                }
                else
                {
                    var serializedObject = NewtonsoftJsonSerializer.Instance.Serialize(new TokenResponse
                    {
                        AccessToken = "a",
                        RefreshToken = "r",
                        ExpiresInSeconds = 100,
                        Scope = "b",
                    });
                    response.Content = new StringContent(serializedObject, Encoding.UTF8);
                }

                return response;
            }
        }

        [Fact]
        public void TestFetchTokenAsync_AuthorizationCodeRequest()
        {
            var handler = new FetchTokenMessageHandler();
            handler.AuthorizationCodeTokenRequest = new AuthorizationCodeTokenRequest()
            {
                Code = "c0de",
                RedirectUri = "redIrect",
                Scope = "a"
            };
            MockHttpClientFactory mockFactory = new MockHttpClientFactory(handler);

            var flow = CreateFlow(httpClientFactory: mockFactory);
            var response = flow.FetchTokenAsync("user", handler.AuthorizationCodeTokenRequest,
                CancellationToken.None).Result;
            SubtestTokenResponse(response);
        }

        [Fact]
        public void TestFetchTokenAsync_RefreshTokenRequest()
        {
            var handler = new FetchTokenMessageHandler();
            handler.RefreshTokenRequest = new RefreshTokenRequest()
                {
                    RefreshToken = "REFRESH",
                    Scope = "a"
                };

            MockHttpClientFactory mockFactory = new MockHttpClientFactory(handler);

            var flow = CreateFlow(httpClientFactory: mockFactory);
            var response = flow.FetchTokenAsync("user", handler.RefreshTokenRequest, CancellationToken.None).Result;
            SubtestTokenResponse(response);
        }

        [Fact]
        public void TestFetchTokenAsync_AuthorizationCodeRequest_Error()
        {
            var handler = new FetchTokenMessageHandler();
            handler.AuthorizationCodeTokenRequest = new AuthorizationCodeTokenRequest()
                {
                    Code = "c0de",
                    RedirectUri = "redIrect",
                    Scope = "a"
                };
            handler.Error = true;
            SubtestFetchTokenAsync_Error(handler);
        }

        [Fact]
        public void TestFetchTokenAsync_RefreshTokenRequest_Error()
        {
            var handler = new FetchTokenMessageHandler();
            handler.RefreshTokenRequest = new RefreshTokenRequest()
                {
                    RefreshToken = "REFRESH",
                    Scope = "a"
                };
            handler.Error = true;
            SubtestFetchTokenAsync_Error(handler);
        }

        /// <summary>Subtest for receiving an error token response.</summary>
        /// <param name="handler">The message handler.</param>
        private void SubtestFetchTokenAsync_Error(FetchTokenMessageHandler handler)
        {
            MockHttpClientFactory mockFactory = new MockHttpClientFactory(handler);
            var flow = CreateFlow(httpClientFactory: mockFactory);
            try
            {
                var request =
                    (TokenRequest)handler.AuthorizationCodeTokenRequest ?? (TokenRequest)handler.RefreshTokenRequest;
                var result = flow.FetchTokenAsync("user", request, CancellationToken.None).Result;
                Assert.True(false);
            }
            catch (AggregateException aex)
            {
                var ex = aex.InnerException as TokenResponseException;
                Assert.NotNull(ex);
                var result = ex.Error;
                Assert.Equal(result.Error, Is.EqualTo("error"));
                Assert.Equal(result.ErrorDescription, Is.EqualTo("desc"));
                Assert.Equal(result.ErrorUri, Is.EqualTo("uri"));
            }
        }

        #endregion

        /// <summary>Creates an authorization code flow with the given parameters.</summary>
        /// <param name="dataStore">The data store.</param>
        /// <param name="scopes">The Scopes.</param>
        /// <param name="httpClientFactory">The HTTP client factory. If not set the default will be used.</param>
        /// <returns>Authorization code flow</returns>
        private AuthorizationCodeFlow CreateFlow(IDataStore dataStore = null, IEnumerable<string> scopes = null,
            IHttpClientFactory httpClientFactory = null)
        {
            var secrets = new ClientSecrets() { ClientId = "id", ClientSecret = "secret" };
            var initializer = new AuthorizationCodeFlow.Initializer(AuthorizationCodeUrl, TokenUrl)
            {
                ClientSecrets = secrets,
                HttpClientFactory = httpClientFactory
            };

            if (dataStore != null)
            {
                initializer.DataStore = dataStore;
            }
            if (scopes != null)
            {
                initializer.Scopes = scopes;
            }
            return new AuthorizationCodeFlow(initializer);
        }

        /// <summary>Verifies that the token response contains the expected data.</summary>
        /// <param name="response">The token response</param>
        private void SubtestTokenResponse(TokenResponse response)
        {
            Assert.Equal(response.RefreshToken, Is.EqualTo("r"));
            Assert.Equal(response.ExpiresInSeconds, Is.EqualTo(100));
            Assert.Equal(response.Scope, Is.EqualTo("b"));
        }
    }
}

