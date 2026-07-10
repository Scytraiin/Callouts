using Callouts.Core.Config;

using Xunit;

namespace Callouts.Tests;

public sealed class ConfigMigratorTests
{
    [Fact]
    public void FreshInstall_NullRaw_NoBackupNoNotice()
    {
        var plan = ConfigMigrator.Plan(null, codeVersion: 1);

        Assert.Null(plan.StoredVersion);
        Assert.False(plan.NeedsBackup);
        Assert.False(plan.RefuseAsDowngrade);
        Assert.Null(plan.Notice);
    }

    [Fact]
    public void SameVersion_NoBackupNoNotice()
    {
        var plan = ConfigMigrator.Plan("{\"Version\":1}", codeVersion: 1);

        Assert.Equal(1, plan.StoredVersion);
        Assert.False(plan.NeedsBackup);
        Assert.Null(plan.Notice);
    }

    [Fact]
    public void Upgrade_BacksUpAndAnnounces()
    {
        var plan = ConfigMigrator.Plan("{\"Version\":1}", codeVersion: 3);

        Assert.Equal(1, plan.StoredVersion);
        Assert.True(plan.NeedsBackup);
        Assert.Equal("callouts-config.backup-v1.json", plan.BackupFileName);
        Assert.False(plan.RefuseAsDowngrade);
        Assert.NotNull(plan.Notice);
    }

    [Fact]
    public void Downgrade_RefusesAndBacksUp()
    {
        var plan = ConfigMigrator.Plan("{\"Version\":5}", codeVersion: 2);

        Assert.Equal(5, plan.StoredVersion);
        Assert.True(plan.NeedsBackup);
        Assert.Equal("callouts-config.backup-v5.json", plan.BackupFileName);
        Assert.True(plan.RefuseAsDowngrade);
        Assert.NotNull(plan.Notice);
    }

    [Fact]
    public void CorruptJson_TreatedAsFresh_NoCrash()
    {
        var plan = ConfigMigrator.Plan("{ this is not valid json ", codeVersion: 1);

        Assert.Null(plan.StoredVersion);
        Assert.False(plan.NeedsBackup);
    }

    [Fact]
    public void MissingVersionField_TreatedAsFresh()
    {
        var plan = ConfigMigrator.Plan("{\"Rules\":[]}", codeVersion: 1);

        Assert.Null(plan.StoredVersion);
        Assert.False(plan.NeedsBackup);
    }
}
