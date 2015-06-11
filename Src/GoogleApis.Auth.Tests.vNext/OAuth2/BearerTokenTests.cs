/*
Copyright 2013 Google Inc

Licensed under the Apache License, Version 2.0(the "License");
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
using System.Net.Http.Headers;
using System.Text;

using Xunit;

namespace Google.Apis.Auth.OAuth2
{
    /// <summary>Tests for <see cref="Google.Apis.Auth.OAuth2.BearerToken"/>.</summary>
    public class BearerTokenTests
    {
        [Fact]
        public void AuthorizationHeaderAccessMethod_Intercept()
        {
            var request = new HttpRequestMessage();
            new BearerToken.AuthorizationHeaderAccessMethod().Intercept(request, "abc");
            Assert.Equal(request.Headers.Authorization.ToString(), Is.EqualTo("Bearer abc"));
        }

        [Fact]
        public void AuthorizationHeaderAccessMethod_InterceptOverride()
        {
            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("a", "1");
            new BearerToken.AuthorizationHeaderAccessMethod().Intercept(request, "abc");
            Assert.Equal(request.Headers.Authorization.ToString(), Is.EqualTo("Bearer abc"));
        }

        [Fact]
        public void AuthorizationHeaderAccessMethod_GetAccessToken()
        {
            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("a", "1");
            var accessToken = new BearerToken.AuthorizationHeaderAccessMethod().GetAccessToken(request);
            Assert.True(string.IsNullOrEmpty(accessToken));

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "abc");
            accessToken = new BearerToken.AuthorizationHeaderAccessMethod().GetAccessToken(request);
            Assert.Equal(accessToken, Is.EqualTo("abc"));
        }

        [Fact]
        public void QueryParameterAccessMethod_Intercept()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://sample.com/path"));
            new BearerToken.QueryParameterAccessMethod().Intercept(request, "abc");
            Assert.Equal(request.RequestUri, Is.EqualTo(new Uri("https://sample.com/path?access_token=abc")));
        }

        [Fact]
        public void QueryParameterAccessMethod_Intercept_WithQueryParameters()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://sample.com/path?a=1"));
            new BearerToken.QueryParameterAccessMethod().Intercept(request, "abc");
            Assert.Equal(request.RequestUri, Is.EqualTo(new Uri("https://sample.com/path?a=1&access_token=abc")));
        }

        [Fact]
        public void QueryParameterAccessMethod_GetAccessToken()
        {
            // No query parameter at all.
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://sample.com"));
            var accessToken = new BearerToken.QueryParameterAccessMethod().GetAccessToken(request);
            Assert.True(string.IsNullOrEmpty(accessToken));

            // Different query parameter.
            request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://sample.com?a=1"));
            accessToken = new BearerToken.QueryParameterAccessMethod().GetAccessToken(request);
            Assert.True(string.IsNullOrEmpty(accessToken));

            // One query parameter and it's access_token.
            request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://sample.com?a=1&access_token=abc"));
            accessToken = new BearerToken.QueryParameterAccessMethod().GetAccessToken(request);
            Assert.Equal(accessToken, Is.EqualTo("abc"));

            // 2 query parameters and one of them is access_token.
            request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://sample.com?access_token=abc"));
            accessToken = new BearerToken.QueryParameterAccessMethod().GetAccessToken(request);
            Assert.Equal(accessToken, Is.EqualTo("abc"));
        }
    }
}

