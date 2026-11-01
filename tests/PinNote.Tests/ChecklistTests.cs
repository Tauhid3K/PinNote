using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PinNote.Models;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class ChecklistTests
    {
        [Fact]
        public void ChecklistItemModel_Clone_CreatesExactCopy()
        {
            var item = new ChecklistItemModel
            {
                Id = Guid.NewGuid(),
                Text = "Milk",
                IsChecked = true,
                Order = 3
            };

            var clone = item.Clone();

            Assert.Equal(item.Id, clone.Id);
            Assert.Equal(item.Text, clone.Text);
            Assert.Equal(item.IsChecked, clone.IsChecked);
            Assert.Equal(item.Order, clone.Order);
        }

        [Fact]
        public void NoteModel_WithChecklist_RoundtripsThroughJson()
        {
            var note = new NoteModel
            {
                Title = "Grocery List",
                NoteType = NoteType.Checklist,
                ChecklistItems = new List<ChecklistItemModel>
                {
                    new() { Text = "Apples", IsChecked = false, Order = 0 },
                    new() { Text = "Bread", IsChecked = true, Order = 1 }
                }
            };

            string json = JsonConvert.SerializeObject(note);
            var deserialized = JsonConvert.DeserializeObject<NoteModel>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(NoteType.Checklist, deserialized.NoteType);
            Assert.Equal(2, deserialized.ChecklistItems.Count);
            Assert.Equal("Apples", deserialized.ChecklistItems[0].Text);
            Assert.False(deserialized.ChecklistItems[0].IsChecked);
            Assert.Equal("Bread", deserialized.ChecklistItems[1].Text);
            Assert.True(deserialized.ChecklistItems[1].IsChecked);
        }

        [Fact]
        public void OlderJson_WithoutChecklistFields_LoadsAsTextNote()
        {
            // Simulate older version JSON
            string legacyJson = "{\"Id\":\"" + Guid.NewGuid() + "\",\"Title\":\"Legacy Note\",\"Content\":\"Plain text\"}";

            var deserialized = JsonConvert.DeserializeObject<NoteModel>(legacyJson);

            Assert.NotNull(deserialized);
            Assert.Equal(NoteType.Text, deserialized.NoteType);
            Assert.NotNull(deserialized.ChecklistItems);
            Assert.Empty(deserialized.ChecklistItems);
            Assert.Equal("Plain text", deserialized.Content);
        }

        [Fact]
        public void NoteViewModel_ChecklistItems_CanAddAndRemove()
        {
            var model = new NoteModel { NoteType = NoteType.Checklist };
            var mainVm = new MainViewModel();
            var vm = new NoteViewModel(model, mainVm);

            vm.AddChecklistItem("Item 1");
            vm.AddChecklistItem("Item 2");

            Assert.Equal(2, vm.ChecklistItems.Count);
            Assert.Equal("Item 1", vm.ChecklistItems[0].Text);
            Assert.Equal("Item 2", vm.ChecklistItems[1].Text);

            vm.RemoveChecklistItem(vm.ChecklistItems[0]);

            Assert.Single(vm.ChecklistItems);
            Assert.Equal("Item 2", vm.ChecklistItems[0].Text);
            Assert.Equal(0, vm.ChecklistItems[0].Order);
        }

        [Fact]
        public void NoteViewModel_ChecklistItems_CanReorder()
        {
            var model = new NoteModel { NoteType = NoteType.Checklist };
            var mainVm = new MainViewModel();
            var vm = new NoteViewModel(model, mainVm);

            vm.AddChecklistItem("First");
            vm.AddChecklistItem("Second");

            // Move second up
            vm.MoveChecklistItemUp(vm.ChecklistItems[1]);

            Assert.Equal("Second", vm.ChecklistItems[0].Text);
            Assert.Equal("First", vm.ChecklistItems[1].Text);
            Assert.Equal(0, vm.ChecklistItems[0].Order);
            Assert.Equal(1, vm.ChecklistItems[1].Order);

            // Move first (now index 0) down
            vm.MoveChecklistItemDown(vm.ChecklistItems[0]);

            Assert.Equal("First", vm.ChecklistItems[0].Text);
            Assert.Equal("Second", vm.ChecklistItems[1].Text);
        }

        [Fact]
        public void NoteViewModel_ToggleNoteType_ConvertsTextToChecklistAndBack()
        {
            var model = new NoteModel
            {
                NoteType = NoteType.Text,
                Content = "Line 1\n- [x] Done item\nLine 3"
            };
            var mainVm = new MainViewModel();
            var vm = new NoteViewModel(model, mainVm);

            // Convert to checklist
            vm.ToggleNoteType();

            Assert.True(vm.IsChecklist);
            Assert.Equal(3, vm.ChecklistItems.Count);
            Assert.Equal("Line 1", vm.ChecklistItems[0].Text);
            Assert.False(vm.ChecklistItems[0].IsChecked);
            Assert.Equal("Done item", vm.ChecklistItems[1].Text);
            Assert.True(vm.ChecklistItems[1].IsChecked);
            Assert.Equal("Line 3", vm.ChecklistItems[2].Text);
            Assert.False(vm.ChecklistItems[2].IsChecked);

            // Convert back to text
            vm.ToggleNoteType();

            Assert.False(vm.IsChecklist);
            Assert.Contains("[ ] Line 1", vm.Content);
            Assert.Contains("[x] Done item", vm.Content);
            Assert.Contains("[ ] Line 3", vm.Content);
        }
    }
}
