/*
Copyright 2010 Google Inc

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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Google.Apis.Discovery;
using Google.Apis.Json;
using Google.Apis.JSON;
using Google.Apis.Requests;
using Google.Apis.Testing;
using Google.Apis.Util;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Google.Apis.Tests.Apis.Discovery
{
    /// <summary>
    /// Test for the "BaseService" class.
    /// </summary>
    [TestFixture]
    public class BaseServiceTest
    {
        private class ConcreteClass : BaseService
        {
            public ConcreteClass(string version, string name, JsonDictionary js)
                : base(version, name, js, new ConcreteFactoryParameters()) {}

            public override DiscoveryVersion DiscoveryVersion
            {
                get { throw new NotImplementedException(); }
            }

            public new string ServerUrl 
            {
                get { return base.ServerUrl; }
                set { base.ServerUrl = value; }
            }
            public new string BasePath
            {
                get { return base.BasePath; }
                set { base.BasePath = value; }
            } 

            #region Nested type: ConcreteFactoryParameters

            private class ConcreteFactoryParameters : BaseFactoryParameters
            {
                public ConcreteFactoryParameters() : base("http://test/", "testService/") {}
            }

            #endregion
        }

        /// <summary>
        /// A Json schema for testing serialization/deserialization.
        /// </summary>
        internal class MockJsonSchema : IResponse
        {
            [JsonProperty("kind")]
            public string Kind { get; set; }

            [JsonProperty("longUrl")]
            public string LongURL { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            public RequestError Error { get; set; }
        }

        #region Test Helper methods

        private void CheckDeserializationResults(MockJsonSchema result)
        {
            Assert.NotNull(result);
            Assert.That(result.Kind, Is.EqualTo("urlshortener#url"));
            Assert.That(result.LongURL, Is.EqualTo("http://google.com/"));
            Assert.That(result.Status, Is.Null);
        }

        private IService CreateV1Service()
        {
            var dict = new JsonDictionary();
            return new ConcreteClass("V1", "NameTest", dict);
        }

        private IService CreateLegacyV03Service()
        {
            var dict = new JsonDictionary();
            dict.Add("features", new[] { Features.LegacyDataResponse.GetStringValue() });
            return new ConcreteClass("V1", "NameTest", dict);
        }

        #endregion

        /// <summary>
        /// This tests the v0.3 Deserialization of the BaseService.
        /// </summary>
        [Test]
        public void TestDeserializationV0_3()
        {
            const string ResponseV0_3 =
                @"{ ""data"" : 
                     {
                        ""kind"": ""urlshortener#url"",
                        ""longUrl"": ""http://google.com/"",
                     } 
                  }";

            IService impl = CreateLegacyV03Service();

            // Check that the default serializer is set.
            Assert.IsInstanceOf<NewtonsoftJsonSerializer>(impl.Serializer);

            // Check that the response is decoded correctly.
            var stream = new MemoryStream(Encoding.Default.GetBytes(ResponseV0_3));
            CheckDeserializationResults(impl.DeserializeResponse<MockJsonSchema>(stream));
        }

        /// <summary>
        /// This tests the v1 Deserialization of the BaseService.
        /// </summary>
        [Test]
        public void TestDeserializationV1()
        {
            const string ResponseV1 = @"{""kind"":""urlshortener#url"",""longUrl"":""http://google.com/""}";

            IService impl = CreateV1Service();

            // Check that the default serializer is set
            Assert.IsInstanceOf<NewtonsoftJsonSerializer>(impl.Serializer);

            // Check that the response is decoded correctly
            var stream = new MemoryStream(Encoding.Default.GetBytes(ResponseV1));
            CheckDeserializationResults(impl.DeserializeResponse<MockJsonSchema>(stream));
        }

        /// <summary>
        /// Confirms that the serializer won't do anything if a string is the requested response type.
        /// </summary>
        [Test]
        public void TestDeserializationString()
        {
            const string ResponseV1 = @"{""kind"":""urlshortener#url"",""longUrl"":""http://google.com/""}";

            IService impl = CreateV1Service();

            // Check that the response is decoded correctly
            var stream = new MemoryStream(Encoding.Default.GetBytes(ResponseV1));
            string result = impl.DeserializeResponse<string>(stream);
            Assert.AreEqual(ResponseV1, result);
        }

        /// <summary>
        /// Tests the deserialization for server error responses.
        /// </summary>
        [Test]
        public void TestErrorDeserialization()
        {
            const string ErrorResponse =
                @"{
                    ""error"": {
                        ""errors"": [
                            {
                                ""domain"": ""global"",
                                ""reason"": ""required"",
                                ""message"": ""Required"",
                                ""locationType"": ""parameter"",
                                ""location"": ""resource.longUrl""
                            }
                        ],
                        ""code"": 400,
                        ""message"": ""Required""
                    }
                }";

            foreach (DiscoveryVersion v in new[] { DiscoveryVersion.Version_1_0, DiscoveryVersion.Version_0_3 })
            {
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(ErrorResponse)))
                {
                    IService impl = (v == DiscoveryVersion.Version_1_0 ? CreateV1Service() : CreateLegacyV03Service());

                    // Verify that the response is decoded correctly.
                    try
                    {
                        impl.DeserializeResponse<MockJsonSchema>(stream);
                        Assert.Fail("GoogleApiException was not thrown for invalid Json");
                    }
                    catch (GoogleApiException ex)
                    {
                        // Check that the contents of the error json was translated into the exception object.
                        // We cannot compare the entire exception as it depends on the implementation and might change.
                        Assert.That(ex.ToString(), Contains.Substring("resource.longUrl"));
                    }
                }
            }
        }

        /// <summary>
        /// This tests the "Features" extension of services.
        /// </summary>
        [Test]
        public void TestFeaturesV1()
        {
            IService impl = CreateV1Service();
            Assert.NotNull(impl.Features);
            Assert.IsFalse(impl.HasFeature(Features.LegacyDataResponse));
        }

        /// <summary>
        /// This test is designed to test the "Features" extension of services.
        /// </summary>
        [Test]
        public void TestFeaturesV03()
        {
            IService impl = CreateLegacyV03Service();
            Assert.NotNull(impl.Features);
            Assert.IsTrue(impl.HasFeature(Features.LegacyDataResponse));
        }

        /// <summary>
        /// This test confirms that the BaseService will not crash on non-existent, optional fields
        /// within the JSON document.
        /// </summary>
        [Test]
        public void TestNoThrowOnFieldsMissing()
        {
            IService impl = CreateV1Service();

            Assert.AreEqual("V1", impl.Version);
            Assert.IsNull(impl.Id);
            Assert.IsNotNull(impl.Labels);
            MoreAsserts.IsEmpty(impl.Labels);
            Assert.AreEqual("NameTest", impl.Name);
            Assert.IsNull(impl.Protocol);
            Assert.IsNull(impl.Title);
        }

        /// <summary>
        /// Tests if serialization works.
        /// </summary>
        [Test]
        public void TestSerializationV0_3()
        {
            const string ResponseV0_3 =
                "{\"data\":{\"kind\":\"urlshortener#url\",\"longUrl\":\"http://google.com/\"}}";

            MockJsonSchema schema = new MockJsonSchema();
            schema.Kind = "urlshortener#url";
            schema.LongURL = "http://google.com/";

            IService impl = CreateLegacyV03Service();

            // Check if a response is serialized correctly
            string result = impl.SerializeRequest(schema);
            Assert.AreEqual(ResponseV0_3, result);
        }

        /// <summary>
        /// Tests if serialization works.
        /// </summary>
        [Test]
        public void TestSerializationV1()
        {
            const string ResponseV1 = @"{""kind"":""urlshortener#url"",""longUrl"":""http://google.com/""}";

            MockJsonSchema schema = new MockJsonSchema();
            schema.Kind = "urlshortener#url";
            schema.LongURL = "http://google.com/";

            IService impl = CreateV1Service();

            // Check if a response is serialized correctly
            string result = impl.SerializeRequest(schema);
            Assert.AreEqual(ResponseV1, result);
        }

        /// <summary>
        /// The test targets the more basic properties of the BaseService.
        /// It should ensure that all properties return the values assigned to them within the JSON document.
        /// </summary>
        [Test]
        public void TestSimpleGetters()
        {
            var dict = new JsonDictionary();
            dict.Add("description", "Test Description");
            dict.Add("documentationLink", "https://www.google.com/");
            dict.Add("features", new ArrayList { "feature1", "feature2" });
            dict.Add("labels", new ArrayList { "label1", "label2" });
            dict.Add("id", "TestId");
            dict.Add("title", "Test API");

            IService impl = new ConcreteClass("V1", "NameTest", dict);
            Assert.AreEqual("Test Description", impl.Description);
            Assert.AreEqual("https://www.google.com/", impl.DocumentationLink);
            MoreAsserts.ContentsEqualAndInOrder(new List<string> { "feature1", "feature2" }, impl.Features);
            MoreAsserts.ContentsEqualAndInOrder(new List<string> { "label1", "label2" }, impl.Labels);
            Assert.AreEqual("TestId", impl.Id);
            Assert.AreEqual("Test API", impl.Title);
        }

        /// <summary>
        /// Confirms that OAuth2 scopes can be parsed correctly.
        /// </summary>
        [Test]
        public void TestOAuth2Scopes()
        {
            var scopes = new JsonDictionary();
            scopes.Add("https://www.example.com/auth/one", new Scope() { ID = "https://www.example.com/auth/one" });
            scopes.Add("https://www.example.com/auth/two", new Scope() { ID = "https://www.example.com/auth/two" });
            var oauth2 = new JsonDictionary() { { "scopes", scopes } };
            var auth = new JsonDictionary() { { "oauth2", oauth2 } };
            var dict = new JsonDictionary() { { "auth", auth } };

            IService impl = new ConcreteClass("V1", "NameTest", dict);
            Assert.IsNotNull(impl.Scopes);
            Assert.AreEqual(2, impl.Scopes.Count);
            Assert.IsTrue(impl.Scopes.ContainsKey("https://www.example.com/auth/one"));
            Assert.IsTrue(impl.Scopes.ContainsKey("https://www.example.com/auth/two"));
        }

        /// <summary>
        /// Tests the BaseResource.GetResource method.
        /// </summary>
        [Test]
        public void TestGetResource()
        {
            var container = CreateV1Service();
            container.Resources.Clear();

            // Create json.
            var subJson = new JsonDictionary();
            subJson.Add("resources", new JsonDictionary { { "Grandchild", new JsonDictionary() } });
            var topJson = new JsonDictionary();
            topJson.Add("resources", new JsonDictionary { { "Sub", subJson } });

            // Create the resource hierachy.
            var topResource = new ResourceV1_0(new KeyValuePair<string, object>("Top", topJson));
            var subResource = topResource.Resources["Sub"];
            var grandchildResource = subResource.Resources["Grandchild"];
            container.Resources.Add("Top", topResource); 

            // Check the generated full name.
            Assert.AreEqual(topResource, BaseService.GetResource(container, "Top"));
            Assert.AreEqual(subResource, BaseService.GetResource(container, "Top.Sub"));
            Assert.AreEqual(grandchildResource, BaseService.GetResource(container, "Top.Sub.Grandchild"));
        }

        [Test]
        public void TestBaseUri()
        {
            ConcreteClass instance = (ConcreteClass)CreateV1Service();
            instance.BasePath = "/test/";
            instance.ServerUrl = "https://www.test.value/";
            Assert.AreEqual("https://www.test.value/test/", instance.BaseUri.ToString());

            instance.BasePath = "test/";
            instance.ServerUrl = "https://www.test.value/";
            Assert.AreEqual("https://www.test.value/test/", instance.BaseUri.ToString());

            instance.BasePath = "/test/";
            instance.ServerUrl = "https://www.test.value";
            Assert.AreEqual("https://www.test.value/test/", instance.BaseUri.ToString());

            instance.BasePath = "test/";
            instance.ServerUrl = "https://www.test.value";
            Assert.AreEqual("https://www.test.value/test/", instance.BaseUri.ToString());
            
            // Mono's Uri class strips double forward slashes so this test will not work.
            // Only run for MS.Net
            if ( Google.Apis.Util.Utilities.IsMonoRuntime() == false)
            {
                instance.BasePath = "//test/";
                instance.ServerUrl = "https://www.test.value";
                Assert.AreEqual("https://www.test.value//test/", instance.BaseUri.ToString());
                
                instance.BasePath = "//test/";
                instance.ServerUrl = "https://www.test.value/";
                Assert.AreEqual("https://www.test.value//test/", instance.BaseUri.ToString());
                
                instance.BasePath = "test/";
                instance.ServerUrl = "https://www.test.value//";
                Assert.AreEqual("https://www.test.value//test/", instance.BaseUri.ToString());
                
                instance.BasePath = "/test//";
                instance.ServerUrl = "https://www.test.value/";
                Assert.AreEqual("https://www.test.value/test//", instance.BaseUri.ToString());
            }
        }
    }
}
