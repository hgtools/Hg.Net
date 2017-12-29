using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Mercurial.Tests.Hooks
{
    [TestFixture]
    [Category("Integration")]
    public class PreCommitHookTests : SingleRepositoryTestsBase
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            Repo.Init();
            File.WriteAllText(Path.Combine(Repo.Path, "dummy.txt"), "dummy");
            Repo.Add("dummy.txt");
        }

        [Test]
        public void Commit_HookThatPasses_AllowsCommit()
        {
            Repo.SetHook("precommit", "ok");

            var command = new CustomCommand("commit")
                .WithAdditionalArgument("-m")
                .WithAdditionalArgument("dummy");
            Repo.Execute(command);

            Assert.That(command.RawExitCode, Is.EqualTo(0));
            Assert.That(Repo.Log().Count(), Is.EqualTo(1));
        }

        [Test]
        [Ignore("Waiting for fix: https://github.com/vCipher/Hg.Net/issues/1")]
        public void Commit_HookThatFails_DoesNotAllowCommit()
        {
            Repo.SetHook("precommit", "fail");

            var command = new CustomCommand("commit")
                .WithAdditionalArgument("-m")
                .WithAdditionalArgument("dummy");
            Assert.Throws<MercurialExecutionException>(() => Repo.Execute(command));

            Assert.That(command.RawExitCode, Is.Not.EqualTo(0));
            Assert.That(Repo.Log().Count(), Is.EqualTo(0));
        }
    }
}
