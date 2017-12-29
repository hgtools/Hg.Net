using System.Linq;
using NUnit.Framework;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;

namespace Mercurial.Tests
{
    [TestFixture]
    public class UnicodeTests : SingleRepositoryTestsBase
    {
        private static IEnumerable<string> UnicodeFilenames
        {
            get
            {
                if (Encoding.Default == Encoding.GetEncoding("Windows-1251"))
                {
                    yield return "testжшеЖШЕ.txt";
                }
            }
        }

        private static IEnumerable<string> UnicodeCommitMessages
        {
            get
            {
                if (Encoding.Default == Encoding.GetEncoding("Windows-1251"))
                {
                    yield return "Комментарий на русском языке";
                }
            }
        }

        [Test]
        [TestCaseSource(nameof(UnicodeCommitMessages))]
        [Category("Integration")]
        public void Commit_WithUnicodeCharactersInCommitMessage_ProducesLogMessageWithTheSameText(string commitMessage)
        {
            Repo.Init();
            PrepareMercurialConfig(Repo.Path);
            WriteTextFileAndCommit(Repo, "test.txt", "dummy", commitMessage, true);
            var logEntry = Repo.Log().First();
            string logMessage = Repo.Log().First().CommitMessage;

            Assert.That(logMessage, Is.EqualTo(commitMessage));
        }

        [Test]
        [TestCaseSource(nameof(UnicodeCommitMessages))]
        [Category("Integration")]
        public void Branch_WithUnicodeCharactersInCommitMessage_ProducesLogMessageWithTheSameText(string branchName)
        {
            Repo.Init();
            PrepareMercurialConfig(Repo.Path);
            Repo.Branch(branchName);
            WriteTextFileAndCommit(Repo, "test.txt", "dummy", branchName, true);
            string branchLog = Repo.Log().First().Branch;

            Assert.That(branchLog, Is.EqualTo(branchName));
        }

        [Test]
        [TestCaseSource(nameof(UnicodeFilenames))]
        [Category("Integration")]
        public void Commit_WithUnicodeCharactersInFileName_ProducesLogMessageWithTheSameFileName(string filename)
        {
            Repo.Init();
            PrepareMercurialConfig(Repo.Path);
            WriteTextFileAndCommit(Repo, filename, "dummy", "dummy", true);
            string logFileName = Repo.Log(
                new LogCommand
                {
                    IncludePathActions = true,
                }).First().PathActions.First().Path;

            Assert.That(logFileName, Is.EqualTo(logFileName));
        }

        [Test]
        [TestCaseSource(nameof(UnicodeFilenames))]
        [Category("Integration")]
        public void Add_ExistingFileWithUnicodeCharacterInName_AddsItToTheRepository(string filename)
        {
            Repo.Init();
            PrepareMercurialConfig(Repo.Path);
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
                var terminalEncoding = Console.OutputEncoding;
                writer.WriteLine("[net]");
                writer.WriteLine($"main_encoding 	=  {encoding.WebName}");
                writer.WriteLine($"terminal_encoding 	=  {terminalEncoding.WebName}");
            }

            ClientExecutable.Configuration.Refresh(path);
        }
    }
}