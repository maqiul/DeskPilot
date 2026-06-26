using System.Reflection;
using System.Text.Json;
using DeskPilot.Core.Models;

namespace DeskPilot.Tests;

/// <summary>
/// v0.9: Skill 数据模型 + 默认 JSON 测试。
/// </summary>
public class SkillModelTests
{
    private const string DefaultJsonResource = "DeskPilot.Core.Resources.default-skills.json";

    [Fact]
    public void DefaultSkillsJson_ContainsExactly8Skills()
    {
        var json = LoadEmbeddedJson(DefaultJsonResource);
        var skills = JsonSerializer.Deserialize<List<Skill>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(skills);
        Assert.Equal(8, skills!.Count);
    }

    [Fact]
    public void DefaultSkillsJson_AllHaveRequiredFields()
    {
        var skills = LoadDefaultSkills();
        foreach (var s in skills)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id), $"Id 为空: {s.Name}");
            Assert.False(string.IsNullOrWhiteSpace(s.Name), $"Name 为空: {s.Id}");
            Assert.False(string.IsNullOrWhiteSpace(s.Description), $"Description 为空: {s.Id}");
            Assert.False(string.IsNullOrWhiteSpace(s.Icon), $"Icon 为空: {s.Id}");
            Assert.False(string.IsNullOrWhiteSpace(s.PromptTemplate), $"PromptTemplate 为空: {s.Id}");
            Assert.NotNull(s.Tools);
            Assert.False(string.IsNullOrWhiteSpace(s.Category), $"Category 为空: {s.Id}");
            Assert.True(s.IsEnabled, $"默认应该启用: {s.Id}");
        }
    }

    [Fact]
    public void DefaultSkillsJson_IdsAreUnique()
    {
        var skills = LoadDefaultSkills();
        var ids = skills.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Skill_Roundtrip_PreservesAllFields()
    {
        var original = new Skill(
            Id: "test-skill",
            Name: "测试技能",
            Description: "用于测试",
            Icon: "🧪",
            PromptTemplate: "请帮我测试",
            Tools: new[] { "ToolA", "ToolB" },
            Category: "测试",
            IsEnabled: true);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Skill>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized!.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Description, deserialized.Description);
        Assert.Equal(original.Icon, deserialized.Icon);
        Assert.Equal(original.PromptTemplate, deserialized.PromptTemplate);
        Assert.Equal(original.Tools, deserialized.Tools);
        Assert.Equal(original.Category, deserialized.Category);
        Assert.Equal(original.IsEnabled, deserialized.IsEnabled);
    }

    [Fact]
    public void SkillSet_Enabled_FiltersOutDisabled()
    {
        var set = new SkillSet
        {
            Skills = new()
            {
                new Skill("a", "A", "", "🅰", "", Array.Empty<string>(), "通用", true),
                new Skill("b", "B", "", "🅱", "", Array.Empty<string>(), "通用", false),
                new Skill("c", "C", "", "🅲", "", Array.Empty<string>(), "通用", true),
            }
        };

        var enabled = set.Enabled.ToList();
        Assert.Equal(2, enabled.Count);
        Assert.DoesNotContain(enabled, s => s.Id == "b");
    }

    [Fact]
    public void SkillSet_GroupedByCategory_GroupsCorrectly()
    {
        var set = new SkillSet
        {
            Skills = new()
            {
                new Skill("a1", "A1", "", "🅰", "", Array.Empty<string>(), "文件整理", true),
                new Skill("b1", "B1", "", "🅱", "", Array.Empty<string>(), "图片处理", true),
                new Skill("a2", "A2", "", "🅰", "", Array.Empty<string>(), "文件整理", true),
            }
        };

        var groups = set.GroupedByCategory.ToList();
        Assert.Equal(2, groups.Count);
        var fileGroup = groups.First(g => g.Key == "文件整理");
        Assert.Equal(2, fileGroup.Count());
        var imgGroup = groups.First(g => g.Key == "图片处理");
        Assert.Single(imgGroup);
    }

    [Fact]
    public void DefaultSkillsJson_AllIconFieldsAreSingleEmoji()
    {
        var skills = LoadDefaultSkills();
        foreach (var s in skills)
        {
            // Emoji 通常是 1-2 个 grapheme cluster（带变体选择器）
            Assert.True(s.Icon.Length <= 4, $"Icon 过长（{s.Icon.Length}字符）: {s.Id} → {s.Icon}");
        }
    }

    // ---- v0.10: 技能市场相关断言 ----

    [Fact]
    public void DefaultSkillsJson_AllSkillsAreBuiltIn()
    {
        // v0.10: 默认 8 个技能全部为 IsBuiltIn=true
        var skills = LoadDefaultSkills();
        Assert.Equal(8, skills.Count);
        Assert.All(skills, s => Assert.True(s.IsBuiltIn, $"默认技能应 IsBuiltIn=true: {s.Id}"));
    }

    [Fact]
    public void DefaultSkillsJson_AllSkillsHaveBuiltinSource()
    {
        var skills = LoadDefaultSkills();
        Assert.All(skills, s => Assert.Equal("builtin", s.Source));
    }

    [Fact]
    public void Skill_RecordWithMarketFields_Roundtrips()
    {
        // v0.10 新字段（IsBuiltIn/Source/Version）应参与序列化往返
        var original = new Skill(
            Id: "market-skill",
            Name: "市场技能",
            Description: "从市场安装",
            Icon: "🧩",
            PromptTemplate: "请帮我",
            Tools: new[] { "ToolX" },
            Category: "通用",
            IsEnabled: true,
            IsBuiltIn: false,
            Source: "market:community",
            Version: "1.2.3");

        var json = JsonSerializer.Serialize(original);
        var back = JsonSerializer.Deserialize<Skill>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(back);
        Assert.False(back!.IsBuiltIn);
        Assert.Equal("market:community", back.Source);
        Assert.Equal("1.2.3", back.Version);
    }

    [Fact]
    public void SkillSet_BuiltInAndCustom_DoesNotOverlap()
    {
        // v0.10: BuiltIn 和 Custom 是 SkillSet 的两个 LINQ 视图，应互斥
        var set = new SkillSet
        {
            Skills = new()
            {
                new Skill("a", "A", "", "🅰", "", Array.Empty<string>(), "通用", true, IsBuiltIn: true),
                new Skill("b", "B", "", "🅱", "", Array.Empty<string>(), "通用", true, IsBuiltIn: false),
                new Skill("c", "C", "", "🅲", "", Array.Empty<string>(), "通用", false, IsBuiltIn: true),
            }
        };

        var builtin = set.BuiltIn.Select(s => s.Id).ToList();
        var custom = set.Custom.Select(s => s.Id).ToList();

        Assert.Equal(2, builtin.Count);
        Assert.Contains("a", builtin);
        Assert.Contains("c", builtin);
        Assert.Single(custom);
        Assert.Contains("b", custom);
        // 互斥
        Assert.Empty(builtin.Intersect(custom));
    }

    // ---- helpers ----

    private static List<Skill> LoadDefaultSkills()
    {
        var json = LoadEmbeddedJson(DefaultJsonResource);
        return JsonSerializer.Deserialize<List<Skill>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static string LoadEmbeddedJson(string resourceName)
    {
        var asm = typeof(Skill).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"找不到嵌入资源 {resourceName}。已注册: " +
                string.Join(", ", asm.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
