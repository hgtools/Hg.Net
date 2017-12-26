using System.Linq;
using System.Net;
using NUnit.Framework;

namespace Mercurial.Tests
{
    [TestFixture]
    public class IdentifyTests : SingleRepositoryTestsBase
    {
        [Test]
        [Category("Integration")]
        public void Identify_NoRepository_ThrowsMercurialExecutionException()
        {
            Assert.Throws<MercurialExecutionException>(() => Repo.Identify());
        }

        [Test]
        [Ignore("Ignore while not working")]
        [Category("Integration")]
        public void Identify_RepositoryOverHttp_ReturnsRevSpec()
        {
            IdentifyCommand cmd = new IdentifyCommand().WithPath("https://hg01.codeplex.com/mercurialnet");

            NonPersistentClient.Execute(cmd);

            Assert.That(cmd.Result, Is.Not.Null);
        }

        [Test]
        [Category("Integration")]
        public void Identify_WebSiteThatIsntRepository_ThrowsMercurialExecutionException()
        {
            try
            {
                new WebClient().DownloadString("http://localhost");
            }
            catch (WebException)
            {
                Assert.Inconclusive("No web server set up locally, test not executed");
                return;
            }

            IdentifyCommand cmd = new IdentifyCommand().WithPath("http://localhost");

            Assert.Throws<MercurialExecutionException>(() => NonPersistentClient.Execute(cmd));
        }

        [Test]
        [Category("Integration")]
        public void Identify_WebSiteThatDoesNotExist_ThrowsMercurialExecutionException()
        {
            IdentifyCommand cmd = new IdentifyCommand().WithPath("http://localhostxyzklm");

            Assert.Throws<MercurialExecutionException>(() => NonPersistentClient.Execute(cmd));
        }

        [Test]
        [Category("Integration")]
        public void Identify_WithRepository_ReturnsRevSpec()
        {
            Repo.Init();
            WriteTextFileAndCommit(Repo, "test.txt", "dummy", "dummy", true);

            RevSpec hashViaLog = Repo.Log().First().Revision;
            RevSpec hashViaIdentify = Repo.Identify();

            StringAssert.StartsWith(hashViaIdentify.ToString(), hashViaLog.ToString());
        }

        [Test]
        [Category("Integration")]
        public void Identify_WithRevision_ReturnsRevSpec()
        {
            Repo.Init();
            WriteTextFileAndCommit(Repo, "test.txt", "dummy", "dummy", true);

            var tip = Repo.Tip();
            var revisionViaIdentify = Repo.Identify(
                new IdentifyCommand().WithRevision(tip.Hash));

            StringAssert.StartsWith(revisionViaIdentify.ToString(), tip.Hash);
        }
    }
}