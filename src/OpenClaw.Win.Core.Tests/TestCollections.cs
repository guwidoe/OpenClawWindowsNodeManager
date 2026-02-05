using Xunit;

namespace OpenClaw.Win.Core.Tests;

[CollectionDefinition("StateDir", DisableParallelization = true)]
public sealed class StateDirCollection : ICollectionFixture<StateDirFixture>
{
}

public sealed class StateDirFixture
{
}
