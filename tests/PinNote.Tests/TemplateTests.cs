using System;
using System.IO;
using System.Linq;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class TemplateTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;
        private readonly MainViewModel _vm;
        private readonly TemplateService _templateService;

        public TemplateTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_tpl_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storage = new StorageService(_testDir);
            _vm = new MainViewModel(customStorageService: _storage);
            _templateService = new TemplateService(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void TemplateService_ProvidesAllRequiredBuiltInTemplates()
        {
            var templates = TemplateService.GetBuiltInTemplates();

            Assert.Contains(templates, t => t.Name == "Lecture Notes");
            Assert.Contains(templates, t => t.Name == "Exam Preparation");
            Assert.Contains(templates, t => t.Name == "Daily Planner");
            Assert.Contains(templates, t => t.Name == "Meeting Notes");
            Assert.Contains(templates, t => t.Name == "Project Planning");
            Assert.Contains(templates, t => t.Name == "Personal To-do List");
        }

        [Fact]
        public void MainViewModel_CreateNoteFromTemplate_InstantiatesSeparateNote()
        {
            var lectureTemplate = _vm.Templates.First(t => t.Name == "Lecture Notes");

            int initialNoteCount = _vm.Notes.Count;
            _vm.CreateNoteFromTemplate(lectureTemplate);

            Assert.Equal(initialNoteCount + 1, _vm.Notes.Count);
            var newNote = _vm.Notes.Last();
            Assert.Equal("Lecture Notes", newNote.Title);
            Assert.Equal(NoteType.Markdown, newNote.NoteType);
            Assert.Contains("Lecture", newNote.Tags);
            Assert.Equal("Study", newNote.Category);
        }

        [Fact]
        public void MainViewModel_CreateCustomTemplateFromNote_AndDuplicate()
        {
            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();
            note.Title = "Chemistry Lab";
            note.Content = "# Lab Report\nSafety checks and observations";
            note.AddTag("Chemistry");
            note.Category = "Study";

            _vm.CreateTemplateFromNote(note);

            var createdTpl = _vm.Templates.FirstOrDefault(t => t.Name == "Chemistry Lab Template");
            Assert.NotNull(createdTpl);
            Assert.False(createdTpl.IsBuiltIn);
            Assert.Contains("Chemistry", createdTpl.Tags);

            // Duplicate template
            _vm.DuplicateTemplate(createdTpl);
            var dupTpl = _vm.Templates.FirstOrDefault(t => t.Name == "Copy of Chemistry Lab Template");
            Assert.NotNull(dupTpl);

            // Delete template
            _vm.DeleteTemplate(dupTpl);
            Assert.DoesNotContain(_vm.Templates, t => t.Name == "Copy of Chemistry Lab Template");
        }
    }
}
