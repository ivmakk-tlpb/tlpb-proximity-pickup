using ProximityPickup;
using Xunit;

// The patch classes and the fake game types hold static state, so their tests do not run in parallel.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GameState
{
    public const string Name = "Game state";
}

// Each test starts from a clean state with a loaded plugin. This runs for each test: a class fixture loads once, and
// the next reset leaves the config entries null.
[Collection(GameState.Name)]
public abstract class GameTestBase
{
    protected GameTestBase()
    {
        StaticState.Reset();
        FakeState.Reset();
        new Plugin().Load();
    }
}
