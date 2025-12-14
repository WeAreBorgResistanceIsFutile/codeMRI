using NUnit.Framework;
using codeMRI.Frontend.Services;
using codeMRI.Server.Api;
using System.Collections.Generic;

namespace codeMRI.Frontend.Tests
{
    [TestFixture]
    public class AppStateTests
    {
        private AppState _appState;
        private bool _wasNotified;

        [SetUp]
        public void Setup()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<codeMRI.Frontend.Services.AppState>();
        _appState = new AppState(logger);
            _wasNotified = false;
            _appState.OnChange += () => _wasNotified = true;
        }

        [Test]
        public void SetRepoPath_UpdatesPropertyAndNotifies()
        {
            var newPath = "/new/path";
            _appState.SetRepoPath(newPath);

            Assert.That(_appState.RepoPath, Is.EqualTo(newPath));
            Assert.That(_wasNotified, Is.True);
        }

        [Test]
        public void SetBusy_UpdatesPropertyAndNotifies()
        {
            _appState.SetBusy(true, "Working...");

            Assert.That(_appState.IsBusy, Is.True);
            Assert.That(_appState.BusyMessage, Is.EqualTo("Working..."));
            Assert.That(_wasNotified, Is.True);
        }

        [Test]
        public void SetStructure_UpdatesPropertyAndNotifies()
        {
            var structure = new WikiStructure { Sections = new List<WikiSection>() };
            _appState.SetStructure(structure);

            Assert.That(_appState.Structure, Is.EqualTo(structure));
            Assert.That(_wasNotified, Is.True);
        }

        [Test]
        public void AddChatMessage_AddsMessageAndNotifies()
        {
            var msg = new ChatMessage { Role = "user", Content = "Hello" };
            _appState.AddChatMessage(msg);

            Assert.That(_appState.ChatHistory, Contains.Item(msg));
            Assert.That(_wasNotified, Is.True);
        }

        [Test]
        public void UpdateLastChatMessage_UpdatesContentAndNotifies()
        {
            var msg = new ChatMessage { Role = "assistant", Content = "Initial" };
            _appState.AddChatMessage(msg);
            _wasNotified = false; // Reset

            _appState.UpdateLastChatMessage("Updated");

            Assert.That(_appState.ChatHistory[0].Content, Is.EqualTo("Updated"));
            Assert.That(_wasNotified, Is.True);
        }
    }
}
