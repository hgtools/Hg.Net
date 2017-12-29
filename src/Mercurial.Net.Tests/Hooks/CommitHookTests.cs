using System;
using System.IO;
using NUnit.Framework;

namespace Mercurial.Tests.Hooks
{
    [TestFixture]
    [Category("Integration")]
    public class CommitHookTests : SingleRepositoryTestsBase
    {
        [Test]
        [Ignore("Waiting for fix: https://github.com/vCipher/Hg.Net/issues/1")]
        public void Commit_SingleChangeset_OutputsThatChangeset()
        {
            Repo.Init();
            Repo.SetHook("commit");

            File.WriteAllText(Path.Combine(Repo.Path, "test.txt"), "dummy");
            Repo.Add("test.txt");

            var command = new CustomCommand("commit")
                .WithAdditionalArgument("-m")
                .WithAdditionalArgument("dummy");
            Repo.Execute(command);

            var tipHash = Repo.Tip().Hash;

            Assert.That(command.RawExitCode, Is.EqualTo(0));
            Assert.That(command.RawStandardOutput, Contains.Substring("LeftParentRevision:0000000000000000000000000000000000000000"));
            Assert.That(command.RawStandardOutput, Contains.Substring("RightParentRevision:" + Environment.NewLine));
            Assert.That(command.RawStandardOutput, Contains.Substring("CommittedRevision:" + tipHash));
        }
    }
}