/*
Copyright 2011 Google Inc

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
using Google.Apis.Util;
using Xunit;

namespace Google.Apis.Tests.Apis.Utils
{
    /// <summary>
    /// Tests for the StringValue attribute.
    /// </summary>
    public class StringValueAttributeTest
    {
        /// <summary>
        /// Checks that the construtor can be used, and that properties are properly set.
        /// </summary>
        [Fact]
        public void ConstructTest()
        {
            // Test parameter validation/
            Assert.Throws<ArgumentNullException>(() => new StringValueAttribute(null));
            
            // Test normal operation.
            var attribute = new StringValueAttribute("FooBar");
            Assert.Equal("FooBar", attribute.Text);
        }
    }
}
