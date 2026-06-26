using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DeskPilot.Core.Models;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>v0.12: SkillStep / Skill.Steps 字段 JSON 序列化 + 多步判定测试。</summary>
public class SkillStepTests
{
    [Fact]
    public void SkillStep_DefaultCtor_FieldsPopulate()
    {
        var step = new SkillStep(
            ToolName: "ArchiveByDate",
            Args: new Dictionary<string, object?> { ["path"] = "D:\\Downloads", ["mode"] = "month" },
            Description: "按月份归档下载文件夹",
            Optional: false);
        Assert.Equal("ArchiveByDate", step.ToolName);
        Assert.Equal("D:\\Downloads", step.Args["path"]);
        Assert.Equal("month", step.Args["mode"]);
        Assert.Equal("按月份归档下载文件夹", step.Description);
        Assert.False(step.Optional);
    }

    [Fact]
    public void Skill_Steps_DefaultsToNull_BackwardCompatible()
    {
        var skill = new Skill(
            Id: "old", Name: "老技能", Description: "x",
            Icon: "📦", PromptTemplate: "do it",
            Tools: new[] { "ArchiveByDate" });
        Assert.Null(skill.Steps);
        Assert.False(skill.IsMultiStep);
        Assert.Empty(skill.SafeSteps);
    }

    [Fact]
    public void Skill_Steps_EmptyList_NotMultiStep()
    {
        var skill = new Skill(
            Id: "empty", Name: "空步骤", Description: "x",
            Icon: "📦", PromptTemplate: "do it",
            Tools: Array.Empty<string>(),
            Steps: Array.Empty<SkillStep>());
        Assert.NotNull(skill.Steps);
        Assert.Empty(skill.Steps!);
        Assert.False(skill.IsMultiStep);
    }

    [Fact]
    public void Skill_Steps_NonEmpty_IsMultiStep()
    {
        var skill = new Skill(
            Id: "multi", Name: "多步", Description: "x",
            Icon: "⚡", PromptTemplate: "",
            Tools: new[] { "ArchiveByDate", "HashFiles" },
            Steps: new[]
            {
                new SkillStep("ArchiveByDate", new Dictionary<string, object?> { ["path"] = "/a" }, "归档 A"),
                new SkillStep("HashFiles", new Dictionary<string, object?> { ["path"] = "/b" }, "哈希 B"),
            });
        Assert.True(skill.IsMultiStep);
        Assert.Equal(2, skill.SafeSteps.Count);
        Assert.Equal("ArchiveByDate", skill.SafeSteps[0].ToolName);
        Assert.Equal("/b", skill.SafeSteps[1].Args["path"]);
    }

    [Fact]
    public void Skill_JsonDeserialize_WithoutStepsField_BackwardCompat()
    {
        // 模拟 v0.9/v0.10/v0.11 旧 JSON（无 steps 字段）
        var json = """
        {
            "id": "organize-downloads",
            "name": "整理下载文件夹",
            "description": "按文件类型自动归类下载文件夹里的内容",
            "icon": "📁",
            "promptTemplate": "请帮我整理 D:\\Downloads 文件夹",
            "tools": ["ArchiveByDate"],
            "category": "文件整理",
            "isEnabled": true,
            "isBuiltIn": true,
            "source": "builtin",
            "version": ""
        }
        """;
        var skill = JsonSerializer.Deserialize<Skill>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(skill);
        Assert.Equal("organize-downloads", skill!.Id);
        Assert.Null(skill.Steps);
        Assert.False(skill.IsMultiStep);  // 向后兼容：旧技能走 PromptTemplate 路径
    }

    [Fact]
    public void Skill_JsonDeserialize_WithStepsField_NewMultiStep()
    {
        // v0.12 新格式：带 steps 字段
        var json = """
        {
            "id": "scan-invoices",
            "name": "扫描发票并归档",
            "description": "扫描 Documents/发票/ 下的 PDF/图片，按月份归档",
            "icon": "🧾",
            "promptTemplate": "",
            "tools": ["FindFiles", "ExtractArchive", "MoveFiles"],
            "category": "财务办公",
            "isBuiltIn": false,
            "source": "market:community",
            "version": "1.0.0",
            "steps": [
                { "toolName": "FindFiles", "args": { "path": "Documents/发票/", "pattern": "*.pdf" }, "description": "查找发票 PDF", "optional": false },
                { "toolName": "MoveFiles", "args": { "targetDir": "Documents/发票/{year}/{month}" }, "description": "按月份归档", "optional": true }
            ]
        }
        """;
        var skill = JsonSerializer.Deserialize<Skill>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(skill);
        Assert.Equal("scan-invoices", skill!.Id);
        Assert.True(skill.IsMultiStep);
        Assert.Equal(2, skill.SafeSteps.Count);
        Assert.Equal("FindFiles", skill.SafeSteps[0].ToolName);
        // v0.12: JSON 反序列化 object? 字段会装箱为 JsonElement，需要 GetString() 取值
        var pattern = skill.SafeSteps[0].Args["pattern"];
        Assert.Equal("*.pdf", pattern is System.Text.Json.JsonElement je ? je.GetString() : pattern);
        Assert.True(skill.SafeSteps[1].Optional);
    }

    [Fact]
    public void SkillSet_MultiStep_FiltersCorrectly()
    {
        var set = new SkillSet
        {
            Skills = new List<Skill>
            {
                new("a", "A", "", "📦", "p", Array.Empty<string>(),
                    Steps: new[] { new SkillStep("T", new Dictionary<string, object?>(), "step") }),
                new("b", "B", "", "📦", "p", Array.Empty<string>()),
            }
        };
        var multi = set.MultiStep.ToList();
        Assert.Single(multi);
        Assert.Equal("a", multi[0].Id);
    }

    [Fact]
    public void SkillStep_Optional_DefaultFalse()
    {
        var step = new SkillStep("T", new Dictionary<string, object?>());
        Assert.False(step.Optional);
        Assert.Equal("", step.Description);
    }

    // ============ v0.12.3 community 多步技能 JSON 反序列化测试 ============

    private static string LoadSkillJson(string id)
    {
        // 从仓库根目录的 skills/{id}.json 加载真实 market JSON
        var path = System.IO.Path.Combine(GetRepoRoot(), "skills", $"{id}.json");
        return System.IO.File.ReadAllText(path);
    }

    private static string GetRepoRoot()
    {
        // 探测式向上找含 skills/ 目录的仓库根（最稳：跨 Debug/Release / net8.0-windows 等深度差异）
        var dir = System.AppContext.BaseDirectory;
        for (int i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(dir, "skills")) &&
                System.IO.Directory.Exists(System.IO.Path.Combine(dir, "src")))
                return dir;
            var parent = System.IO.Directory.GetParent(dir);
            dir = parent?.FullName ?? string.Empty;
        }
        throw new System.IO.DirectoryNotFoundException(
            $"找不到仓库根（含 skills/ 和 src/）。BaseDirectory={System.AppContext.BaseDirectory}");
    }

    [Fact]
    public void CommunitySkill_ScanInvoices_LoadsMultiStep()
    {
        var json = LoadSkillJson("scan-invoices");
        var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(skill);
        Assert.Equal("scan-invoices", skill!.Id);
        Assert.True(skill.IsMultiStep);
        Assert.Equal(3, skill.SafeSteps.Count);
        // Step1: HashFiles（required）
        Assert.Equal("HashFiles", skill.SafeSteps[0].ToolName);
        Assert.False(skill.SafeSteps[0].Optional);
        // Step2: ArchiveByDate（required）
        Assert.Equal("ArchiveByDate", skill.SafeSteps[1].ToolName);
        Assert.False(skill.SafeSteps[1].Optional);
        // Step3: FindDuplicates（optional）
        Assert.Equal("FindDuplicates", skill.SafeSteps[2].ToolName);
        Assert.True(skill.SafeSteps[2].Optional);
    }

    [Fact]
    public void CommunitySkill_ScanInvoices_ArgsContainExpectedKeys()
    {
        var json = LoadSkillJson("scan-invoices");
        var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(skill);
        // Step1.Args 应含 directory / algorithm / outputCsv
        var s1 = skill!.SafeSteps[0].Args;
        Assert.True(s1.ContainsKey("directory"));
        Assert.True(s1.ContainsKey("algorithm"));
        Assert.True(s1.ContainsKey("outputCsv"));
    }

    [Fact]
    public void CommunitySkill_WeeklyReport_LoadsMultiStep()
    {
        var json = LoadSkillJson("weekly-report-helper");
        var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(skill);
        Assert.Equal("weekly-report-helper", skill!.Id);
        Assert.True(skill.IsMultiStep);
        Assert.Equal(2, skill.SafeSteps.Count);
        Assert.Equal("HashFiles", skill.SafeSteps[0].ToolName);
        Assert.False(skill.SafeSteps[0].Optional);
        Assert.Equal("BatchResizeImage", skill.SafeSteps[1].ToolName);
        Assert.True(skill.SafeSteps[1].Optional); // 配图压缩可选用
    }

    [Fact]
    public void CommunitySkill_GitCommit_LoadsMultiStep()
    {
        var json = LoadSkillJson("git-commit-message");
        var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(skill);
        Assert.Equal("git-commit-message", skill!.Id);
        Assert.True(skill.IsMultiStep);
        Assert.Equal(2, skill.SafeSteps.Count);
        Assert.Equal("HashFiles", skill.SafeSteps[0].ToolName);
        Assert.False(skill.SafeSteps[0].Optional);
        Assert.Equal("RenameByPattern", skill.SafeSteps[1].ToolName);
        Assert.True(skill.SafeSteps[1].Optional); // dry-run 备份可选用
    }

    [Fact]
    public void CommunitySkills_AllHaveIsMultiStepTrue()
    {
        // 3 个 community 多步技能都应该被 SkillSet.MultiStep 视图过滤出来
        var set = new SkillSet
        {
            Skills = new List<Skill>
            {
                System.Text.Json.JsonSerializer.Deserialize<Skill>(
                    LoadSkillJson("scan-invoices"),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!,
                System.Text.Json.JsonSerializer.Deserialize<Skill>(
                    LoadSkillJson("weekly-report-helper"),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!,
                System.Text.Json.JsonSerializer.Deserialize<Skill>(
                    LoadSkillJson("git-commit-message"),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!,
            }
        };
        var multi = set.MultiStep.Select(s => s.Id).OrderBy(x => x).ToList();
        Assert.Equal(3, multi.Count);
        Assert.Equal("git-commit-message", multi[0]);
        Assert.Equal("scan-invoices", multi[1]);
        Assert.Equal("weekly-report-helper", multi[2]);
    }

    [Fact]
    public void CommunitySkill_ScanInvoices_StepDescriptionNotEmpty()
    {
        var json = LoadSkillJson("scan-invoices");
        var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        // v0.12 要求每个 step 都有用户可见的 Description（UI 进度条展示）
        foreach (var step in skill!.SafeSteps)
        {
            Assert.False(string.IsNullOrWhiteSpace(step.Description),
                $"Step {step.ToolName} 必须有非空 Description（UI 进度条用）");
        }
    }

    // ============ v0.13 community 多步技能 JSON 反序列化测试 ============

    [Fact]
    public void CommunitySkill_CodeReviewHelper_LoadsMultiStep()
    {
        var json = LoadSkillJson("code-review-helper");
        var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(skill);
        Assert.Equal("code-review-helper", skill!.Id);
        Assert.True(skill.IsMultiStep);
        Assert.Equal(2, skill.SafeSteps.Count);
        // Step1: SearchContent (required) - 搜 TODO/FIXME/HACK/XXX
        Assert.Equal("SearchContent", skill.SafeSteps[0].ToolName);
        Assert.False(skill.SafeSteps[0].Optional);
        // Step2: TextStats (optional) - 统计代码行数
        Assert.Equal("TextStats", skill.SafeSteps[1].ToolName);
        Assert.True(skill.SafeSteps[1].Optional);
    }

    [Fact]
    public void CommunitySkill_CodeReviewHelper_SearchContentArgsContainExpectedKeys()
    {
        var json = LoadSkillJson("code-review-helper");
        var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(skill);
        var args = skill!.SafeSteps[0].Args;
        Assert.True(args.ContainsKey("directory"));
        Assert.True(args.ContainsKey("pattern"));
        Assert.True(args.ContainsKey("fileFilter"));
        // pattern 字段反序列化为 JsonElement，需 GetString() 取值
        var pattern = args["pattern"];
        Assert.Equal("TODO|FIXME|HACK|XXX", pattern is System.Text.Json.JsonElement je ? je.GetString() : pattern);
    }

    [Fact]
    public void CommunitySkill_FileOrganizer_LoadsMultiStep()
    {
        var json = LoadSkillJson("file-organizer");
        var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(skill);
        Assert.Equal("file-organizer", skill!.Id);
        Assert.True(skill.IsMultiStep);
        Assert.Equal(2, skill.SafeSteps.Count);
        // Step1: SearchContent (required) - 按关键词分类
        Assert.Equal("SearchContent", skill.SafeSteps[0].ToolName);
        Assert.False(skill.SafeSteps[0].Optional);
        // Step2: ArchiveByDate (optional) - 按日期归档
        Assert.Equal("ArchiveByDate", skill.SafeSteps[1].ToolName);
        Assert.True(skill.SafeSteps[1].Optional);
    }

    [Fact]
    public void CommunitySkill_AllHaveIsMultiStepTrue_V0_13()
    {
        // 验证 v0.13 的 2 个新 community 技能全部 IsMultiStep=true 且 SafeSteps 非空
        var ids = new[] { "code-review-helper", "file-organizer" };
        foreach (var id in ids)
        {
            var json = LoadSkillJson(id);
            var skill = System.Text.Json.JsonSerializer.Deserialize<Skill>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            Assert.NotNull(skill);
            Assert.True(skill!.IsMultiStep, $"{id} 应该 IsMultiStep=true");
            Assert.NotEmpty(skill.SafeSteps);
        }
    }
}
