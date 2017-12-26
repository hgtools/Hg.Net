using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mercurial.Gui;
using NUnit.Framework;

namespace Mercurial.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class GuiRepositoryExtensionMethodsTest
    {
        public static IEnumerable<MethodInfo> AllRepositoryExtensionMethods()
        {
            return
                from method in typeof(GuiClient).GetMethods(BindingFlags.Public | BindingFlags.Static)
                where method.IsStatic
                      && !method.Name.EndsWith("Execute")
                let parameters = method.GetParameters()
                where parameters.Length > 1
                      && parameters[0].ParameterType == typeof(Repository)
                select method;
        }

        [Test]
        [Category("API")]
        [TestCaseSource(nameof(AllRepositoryExtensionMethods))]
        public void EnsureAllExtensionMethodsForRepositoryClassEndsTheirNameWithGui(MethodInfo method)
        {
            StringAssert.EndsWith("Gui", method.Name);
        }
    }
}