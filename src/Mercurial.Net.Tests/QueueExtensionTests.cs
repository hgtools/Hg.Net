using System;
using Mercurial.Extensions.Queues;
using NUnit.Framework;

namespace Mercurial.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class QueueExtensionTests
    {
        [Test]
        [Category("API")]
        [Ignore("Ignore while not working on build server")]
        public void OperateOnPatchRepository_ForCommand_AddsTheRightArgument()
        {
            var command = new AddCommand();
            command.OperateOnPatchRepository();

            CollectionAssert.AreEqual(
                command.AdditionalArguments, new[]
                {
                    "--mq",
                });
        }

        [Test]
        [Category("API")]
        public void OperateOnPatchRepository_WithNullCommand_ThrowsArgumentNullException()
        {
            AddCommand command = null;
            Assert.Throws<ArgumentNullException>(() => command.OperateOnPatchRepository());
        }
    }
}