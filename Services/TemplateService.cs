using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PinNote.Models;

namespace PinNote.Services
{
    public class TemplateService
    {
        private static readonly object _fileLock = new();
        private readonly string _filePath;

        public TemplateService(string? customFolderPath = null)
        {
            string folder = !string.IsNullOrWhiteSpace(customFolderPath)
                ? customFolderPath
                : GetDefaultStorageFolder();

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            _filePath = Path.Combine(folder, "templates.json");
        }

        public List<NoteTemplateModel> LoadTemplates()
        {
            var builtIns = GetBuiltInTemplates();
            var custom = LoadCustomTemplates();

            var result = new List<NoteTemplateModel>(builtIns);
            result.AddRange(custom);
            return result;
        }

        public List<NoteTemplateModel> LoadCustomTemplates()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                {
                    return new List<NoteTemplateModel>();
                }

                try
                {
                    string json = File.ReadAllText(_filePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new List<NoteTemplateModel>();
                    }

                    var list = JsonConvert.DeserializeObject<List<NoteTemplateModel>>(json);
                    return list ?? new List<NoteTemplateModel>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TemplateService.LoadCustomTemplates error: {ex}");
                    return new List<NoteTemplateModel>();
                }
            }
        }

        public void SaveCustomTemplates(IEnumerable<NoteTemplateModel> templates)
        {
            lock (_fileLock)
            {
                try
                {
                    var customOnly = templates.Where(t => !t.IsBuiltIn).ToList();
                    string json = JsonConvert.SerializeObject(customOnly, Formatting.Indented);
                    string tempPath = $"{_filePath}.tmp_{Guid.NewGuid():N}";
                    File.WriteAllText(tempPath, json);
                    File.Move(tempPath, _filePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TemplateService.SaveCustomTemplates error: {ex}");
                }
            }
        }

        public NoteTemplateModel DuplicateTemplate(NoteTemplateModel source)
        {
            var duplicate = source.Clone();
            duplicate.Id = Guid.NewGuid();
            duplicate.Name = $"Copy of {source.Name}";
            duplicate.IsBuiltIn = false;
            return duplicate;
        }

        public bool ExportTemplates(IEnumerable<NoteTemplateModel> templates, string destinationPath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(templates.ToList(), Formatting.Indented);
                string temp = $"{destinationPath}.tmp_{Guid.NewGuid():N}";
                File.WriteAllText(temp, json);
                File.Move(temp, destinationPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExportTemplates error: {ex}");
                return false;
            }
        }

        public List<NoteTemplateModel> ImportTemplates(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return new List<NoteTemplateModel>();
                string json = File.ReadAllText(sourcePath);
                var list = JsonConvert.DeserializeObject<List<NoteTemplateModel>>(json);
                if (list != null)
                {
                    foreach (var t in list)
                    {
                        t.Id = Guid.NewGuid();
                        t.IsBuiltIn = false;
                    }
                    return list;
                }
                return new List<NoteTemplateModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImportTemplates error: {ex}");
                return new List<NoteTemplateModel>();
            }
        }

        public static List<NoteTemplateModel> GetBuiltInTemplates()
        {
            return new List<NoteTemplateModel>
            {
                new()
                {
                    Id = new Guid("10000000-0000-0000-0000-000000000001"),
                    Name = "Lecture Notes",
                    Description = "Course lectures, key concepts, questions, and action items",
                    NoteType = NoteType.Markdown,
                    Category = "Study",
                    Tags = new List<string> { "Lecture", "Study" },
                    IsBuiltIn = true,
                    Content = @"# Course: 
**Date:** 
**Topic:** 
**Lecturer:** 

## Key Concepts
- 
- 

## Detailed Notes
- 

## Questions & Follow-ups
- [ ] Review lecture slides
- [ ] Read textbook chapter"
                },
                new()
                {
                    Id = new Guid("10000000-0000-0000-0000-000000000002"),
                    Name = "Exam Preparation",
                    Description = "Structured checklist for exam revision and practice",
                    NoteType = NoteType.Checklist,
                    Category = "Study",
                    Tags = new List<string> { "Exam", "Study" },
                    IsBuiltIn = true,
                    ChecklistItems = new List<ChecklistItemModel>
                    {
                        new() { Id = Guid.NewGuid(), Text = "Review syllabus and past exam papers", IsChecked = false, Order = 0 },
                        new() { Id = Guid.NewGuid(), Text = "Summarize core lecture formulas & concepts", IsChecked = false, Order = 1 },
                        new() { Id = Guid.NewGuid(), Text = "Complete 3 practice problem sets", IsChecked = false, Order = 2 },
                        new() { Id = Guid.NewGuid(), Text = "Attend revision / study group session", IsChecked = false, Order = 3 },
                        new() { Id = Guid.NewGuid(), Text = "Timed mock exam simulation", IsChecked = false, Order = 4 }
                    }
                },
                new()
                {
                    Id = new Guid("10000000-0000-0000-0000-000000000003"),
                    Name = "Daily Planner",
                    Description = "Daily top priorities, time blocks, and evening reflection",
                    NoteType = NoteType.Markdown,
                    Category = "Personal",
                    Tags = new List<string> { "Planner", "Daily" },
                    IsBuiltIn = true,
                    Content = @"# Daily Plan

## Top 3 Priorities
1. 
2. 
3. 

## Schedule & Tasks
- [ ] Morning deep work block
- [ ] Midday administrative tasks
- [ ] Afternoon study / project block
- [ ] Evening review & wrap-up

## Notes & Thoughts
- "
                },
                new()
                {
                    Id = new Guid("10000000-0000-0000-0000-000000000004"),
                    Name = "Meeting Notes",
                    Description = "Attendees, agenda, discussion points, and action items",
                    NoteType = NoteType.Markdown,
                    Category = "Work",
                    Tags = new List<string> { "Meeting", "Work" },
                    IsBuiltIn = true,
                    Content = @"# Meeting: 
**Date:** 
**Attendees:** 

## Agenda
1. 
2. 

## Key Decisions
- 

## Action Items
- [ ] Task 1 (Owner: )
- [ ] Task 2 (Owner: )"
                },
                new()
                {
                    Id = new Guid("10000000-0000-0000-0000-000000000005"),
                    Name = "Project Planning",
                    Description = "Milestones, deliverables, timeline, and risk management",
                    NoteType = NoteType.Markdown,
                    Category = "Projects",
                    Tags = new List<string> { "Project", "Planning" },
                    IsBuiltIn = true,
                    Content = @"# Project: 
**Goal:** 
**Target Completion:** 

## Milestones
- [ ] Milestone 1: Requirements & Architecture
- [ ] Milestone 2: MVP Implementation
- [ ] Milestone 3: Testing & Feedback
- [ ] Milestone 4: Final Release

## Deliverables
- 

## Identified Risks & Mitigations
- "
                },
                new()
                {
                    Id = new Guid("10000000-0000-0000-0000-000000000006"),
                    Name = "Personal To-do List",
                    Description = "Everyday checklist for personal tasks and routines",
                    NoteType = NoteType.Checklist,
                    Category = "Personal",
                    Tags = new List<string> { "Todo", "Personal" },
                    IsBuiltIn = true,
                    ChecklistItems = new List<ChecklistItemModel>
                    {
                        new() { Id = Guid.NewGuid(), Text = "Grocery shopping & meal prep", IsChecked = false, Order = 0 },
                        new() { Id = Guid.NewGuid(), Text = "Exercise / workout (30+ mins)", IsChecked = false, Order = 1 },
                        new() { Id = Guid.NewGuid(), Text = "Read 20 pages of book", IsChecked = false, Order = 2 },
                        new() { Id = Guid.NewGuid(), Text = "Review emails & clear inbox", IsChecked = false, Order = 3 }
                    }
                }
            };
        }

        private static string GetDefaultStorageFolder()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
            return Path.Combine(root, "PinNote");
        }
    }
}
