using NzbWebDAV.Config;
using NzbWebDAV.Config.Scheduling;
using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Tests.Config;

public class WeeklyWindowScheduleTests
{
    private static ConfigItem Item(string name, string value) =>
        new() { ConfigName = name, ConfigValue = value };

    [Fact]
    public void TryParse_Empty_IsUnrestricted()
    {
        Assert.True(WeeklyWindowSchedule.TryParse("", out var schedule, out var error));
        Assert.Null(error);
        Assert.False(schedule.Enabled);
    }

    [Fact]
    public void TryParse_EnabledWithoutWindows_Fails()
    {
        Assert.False(WeeklyWindowSchedule.TryParse("""{"Enabled":true,"Windows":[]}""", out _, out var error));
        Assert.Contains("window", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_EqualStartAndEnd_Fails()
    {
        const string json = """{"Enabled":true,"Windows":[{"Days":[1],"StartMinute":0,"EndMinute":0}]}""";
        Assert.False(WeeklyWindowSchedule.TryParse(json, out _, out var error));
        Assert.Contains("equal", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfigManager_RejectsInvalidSchedule()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ConfigManager.ValidateConfigItems(
                new List<ConfigItem> { Item(ConfigKeys.QueueProcessingSchedule, "{not-json") }));
        Assert.Contains("JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfigManager_InvalidStoredSchedule_IsUnrestricted()
    {
        var config = new ConfigManager();
        typeof(ConfigManager)
            .GetField("_config", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(config, new Dictionary<string, string> { [ConfigKeys.QueueProcessingSchedule] = "{bad" });
        var schedule = config.GetQueueProcessingSchedule();
        Assert.False(schedule.Enabled);
    }

    [Fact]
    public void QueueAdmission_ManualPauseAndSchedule()
    {
        var config = new ConfigManager();
        config.UpdateValues(new List<ConfigItem> { Item(ConfigKeys.QueuePaused, "true") });
        Assert.True(config.IsQueueEffectivelyPaused());
        Assert.Equal("0", config.GetQueuePauseInt());

        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        var minute = local.Hour * 60 + local.Minute;
        var start = (minute + 120) % 1440;
        var end = (start + 60) % 1440;
        if (end == start) end = (start + 1) % 1440;
        var json =
            $$"""{"Enabled":true,"Windows":[{"Days":[0,1,2,3,4,5,6],"StartMinute":{{start}},"EndMinute":{{end}}}]}""";
        config.UpdateValues(new List<ConfigItem>
        {
            Item(ConfigKeys.QueuePaused, "false"),
            Item(ConfigKeys.QueueProcessingSchedule, json),
        });
        Assert.True(config.IsQueueEffectivelyPaused());
        Assert.NotEqual("0", config.GetQueuePauseInt());
        Assert.True(int.Parse(config.GetQueuePauseInt()) > 0);
    }
}
