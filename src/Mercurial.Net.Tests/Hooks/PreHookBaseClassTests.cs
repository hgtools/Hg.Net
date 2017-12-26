using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mercurial.Hooks;
using NUnit.Framework;

namespace Mercurial.Tests.Hooks
{
    [TestFixture]
    [Category("API")]
    [Parallelizable(ParallelScope.All)]
    public class PreHookBaseClassTests
    {
        public static IEnumerable<Type> HookClasses()
        {
            return
                from type in typeof(MercurialHookBase).Assembly.GetTypes()
                where !type.IsAbstract
                   && !type.IsGenericType
                   && type.Name.EndsWith("Hook")
                select type;
        }

        public static IEnumerable<Type> PreHookClasses()
        {
            return
                from type in HookClasses()
                where type.Name.Contains("Pre")
                select type;
        }

        [Test]
        [TestCaseSource(nameof(PreHookClasses))]
        public void AllPreHooksMustDescendFromMercurialControllingHookBase(Type hookType)
        {
            Assert.That(typeof(MercurialControllingHookBase).IsAssignableFrom(hookType), Is.True);
        }
    }
}
