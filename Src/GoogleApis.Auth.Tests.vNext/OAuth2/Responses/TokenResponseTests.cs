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
using System.Text;

using Xunit;
using Newtonsoft.Json;

using Google.Apis.Tests;

namespace Google.Apis.Auth.OAuth2.Responses
{
    /// <summary>
    /// Tests for <seealso cref="Google.Apis.Auth.OAuth2.Responses.TokenResponse"/>.
    /// </summary>
    public class TokenResponseTests
    {
        [Fact]
        public void TestConstructor()
        {
            var response = new TokenResponse();
            Assert.Null(response.AccessToken);
            Assert.Null(response.ExpiresInSeconds);
            Assert.Null(response.RefreshToken);
            Assert.Null(response.Scope);
            Assert.Equal(DateTime.MinValue, response.Issued);
        }

        [Fact]
        public void TestSerializer()
        {
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(
                @"{
                 'access_token': '123',
                 'expires_in': 1000,
                 'refresh_token': '456',
                 'scope': '789'
                }");
            Assert.Equal(tokenResponse.AccessToken, Is.EqualTo("123"));
            Assert.Equal(tokenResponse.RefreshToken, Is.EqualTo("456"));
            Assert.Equal(tokenResponse.Scope, Is.EqualTo("789"));
            Assert.Equal(tokenResponse.ExpiresInSeconds, Is.EqualTo(1000));
        }

        [Fact]
        public void IsExpired()
        {
            var issued = DateTime.Now;
            var newNow = DateTime.Now.AddSeconds(100);

            var mockClock = new MockClock
            {
                Now = newNow
            };

            // Issued not set.
            var response = new TokenResponse();
            Assert.True(response.IsExpired(mockClock));

            // ExpiresInSeconds is not set.
            response = new TokenResponse() { Issued = issued };
            Assert.True(response.IsExpired(mockClock));

            response = new TokenResponse() { AccessToken = "a", ExpiresInSeconds = 1, Issued = issued };
            Assert.True(response.IsExpired(mockClock));

            response = new TokenResponse() { AccessToken = "a", ExpiresInSeconds = 100, Issued = issued };
            Assert.True(response.IsExpired(mockClock));

            response = new TokenResponse() { AccessToken = "a", ExpiresInSeconds = 158, Issued = issued };
            Assert.True(response.IsExpired(mockClock));

            response = new TokenResponse() { AccessToken = "a", ExpiresInSeconds = 159, Issued = issued };
            Assert.True(response.IsExpired(mockClock));

            response = new TokenResponse() { AccessToken = "a", ExpiresInSeconds = 160, Issued = issued };
            Assert.True(response.IsExpired(mockClock));

            response = new TokenResponse() { AccessToken = "a", ExpiresInSeconds = 161, Issued = issued };
            Assert.False(response.IsExpired(mockClock));

            response = new TokenResponse() { AccessToken = "a", ExpiresInSeconds = 162, Issued = issued };
            Assert.False(response.IsExpired(mockClock));
        }
    }
}

