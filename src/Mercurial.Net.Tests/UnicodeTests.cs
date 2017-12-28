using System.Linq;
using NUnit.Framework;
using System.IO;
using System.Text;

namespace Mercurial.Tests
{
    [TestFixture]
    public class UnicodeTests : SingleRepositoryTestsBase
    {
        [Test]
        [Category("Integration")]
        public void Commit_WithUnicodeCharactersInCommitMessage_ProducesLogMessageWithTheSameText()
        {
            const string commitMessage = "Unicode:testжшеЖШЕ.txt";

            Repo.Init();
            PrepareMercurialConfig(Repo.Path);
            WriteTextFileAndCommit(Repo, "testыыыы.txt", "dummy", commitMessage, true);
            var logEntry = Repo.Log().First();
            string logMessage = Repo.Log().First().CommitMessage;

            Assert.That(logMessage, Is.EqualTo(commitMessage));
        }

        [Test]
        [Category("Integration")]
        public void Branch_WithUnicodeCharactersInCommitMessage_ProducesLogMessageWithTheSameText()
        {
            const string commitMessage = "Unicode:testжшеЖШЕ.txt";

            Repo.Init();
            PrepareMercurialConfig(Repo.Path);
            var branchName = "Одинокая ветка сирени";
            Repo.Branch(branchName);
            WriteTextFileAndCommit(Repo, "testыыыы.txt", "dummy", commitMessage, true);
            string branchLog = Repo.Log().First().Branch;

            Assert.That(branchLog, Is.EqualTo(branchName));
        }

        [Test]
        [Category("Integration")]
        public void Commit_WithUnicodeCharactersInFileName_ProducesLogMessageWithTheSameFileName()
        {
            const string fileName = "testжшеЖШЕ.txt";

            Repo.Init();
            PrepareMercurialConfig(Repo.Path);
            WriteTextFileAndCommit(Repo, fileName, "dummy", "dummy", true);
            string logFileName = Repo.Log(
                new LogCommand
                {
                    IncludePathActions = true,
                }).First().PathActions.First().Path;

            Assert.That(logFileName, Is.EqualTo(logFileName));
        }

        [Test]
        [Category("Integration")]
        public void Add_ExistingFileWithUnicodeCharacterInName_AddsItToTheRepository()
        {
            Repo.Init();
            PrepareMercurialConfig(Repo.Path);
            const string filename = "testжшеЖШЕ.txt";
            File.WriteAllText(Path.Combine(Repo.Path, filename), "contents");
            Repo.Add(filename);
            FileStatus[] status = Repo.Status().ToArray();

            CollectionAssert.AreEqual(
                status, new[]
                {
                    new FileStatus(FileState.Added, filename),
                });
        }

        private void PrepareMercurialConfig(string path)
        {
            Directory.CreateDirectory(path + @"\.hg");
            using (var writer = File.CreateText(path + @"\.hg\hgrc"))
            {
                var encoding = Encoding.Default;
                writer.WriteLine("[net]");
                writer.WriteLine($"main_encoding 	=  {encoding.WebName}");
                writer.WriteLine("terminal_encoding 	=  cp866");
            }

            ClientExecutable.Configuration.Refresh(path);
        }
    }
}