// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Reflection;
    using InfoCarrier.Core.Common;
    using Xunit;

    public class UtilsTest
    {
        [Fact]
        public void GetMethodInfo_throws_on_invalid_expression()
        {
            Assert.Throws<ArgumentException>(() => Utils.GetMethodInfo(() => new object()));
        }

        [Fact]
        public void ToDelegate_with_target()
        {
            MethodInfo method = Utils.GetMethodInfo<UtilsTest>(x => x.Return42());
            object fortyTwo = method.ToDelegate<Func<object>>(this).Invoke();
            Assert.Equal(42, fortyTwo);
        }

        [Fact]
        public void ToDelegate_without_target()
        {
            MethodInfo method = Utils.GetMethodInfo(() => StaticReturn42());
            object fortyTwo = method.ToDelegate<Func<object>>().Invoke();
            Assert.Equal(42, fortyTwo);
        }

        private object Return42() => 42;

        private static object StaticReturn42() => 42;
    }
}
