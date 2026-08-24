using NzbWebDAV.Config;
using NzbWebDAV.Config.Scheduling;
using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Tests.Config;

public class HealthWorkSchedulePolicyTests
{
    private static ConfigItem Item(string name, string value) =>
        new() { ConfigName = name, ConfigValue = value };

    [Fact]
    public void ManualRun_OpensChecksOnly()
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        var minute = local.Hour * 60 + local.Minute;
        var start = (minute + 180) % 1440;
        var end = (start + 30) % 1440;
        if (end == start) end = (start + 1) % 1440;
        var closed = $$"""{"Enabled":true,"Windows":[{"Days":[0,1,2,3,4,5,6],"StartMinute":{{start}},"EndMinute":{{end}}}]}""";
        var config = new ConfigManager();
        config.UpdateValues(new List<ConfigItem>
        {
            Item(ConfigKeys.RepairHealthcheckSchedule, closed),
            Item(ConfigKeys.RepairActionSchedule, closed),
        });
        var policy = new HealthWorkSchedulePolicy(config);
        var before = policy.Evaluate(DateTimeOffset.UtcNow);
        Assert.False(before.ChecksOpen);
        Assert.False(before.RepairsOpen);

        Assert.False(policy.BeginManualRun());
        var during = policy.Evaluate(DateTimeOffset.UtcNow);
        Assert.True(during.ChecksOpen);
        Assert.False(during.RepairsOpen);
        Assert.True(during.ManualRunActive);

        Assert.True(policy.BeginManualRun());
        policy.EndManualRun();
        var after = policy.Evaluate(DateTimeOffset.UtcNow);
        Assert.False(after.ChecksOpen);
        Assert.False(after.ManualRunActive);
    }
}
