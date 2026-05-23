using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Runtime.Entities;

namespace Engine.Runtime.Tests.Entities;

/// <summary>Tests that Scene.Draw calls components in ascending Y-position order.</summary>
public class GameObjectDrawOrderTests
{
    // ── Stubs ─────────────────────────────────────────────────────────────────

    /// <summary>No-op ISpriteBatch; components don't need it for order-recording tests.</summary>
    private sealed class StubSpriteBatch : ISpriteBatch
    {
        public void DrawSprite(
            Microsoft.Xna.Framework.Graphics.Texture2D? texture,
            Microsoft.Xna.Framework.Rectangle source,
            Microsoft.Xna.Framework.Rectangle destination,
            Microsoft.Xna.Framework.Color tint) { }
    }

    /// <summary>
    /// On OnDraw, appends the owner's Y position to a shared list so tests can
    /// verify the sequence without coupling to ISpriteBatch internals.
    /// </summary>
    private sealed class YRecordingComponent : Component
    {
        private readonly List<float> _record;
        public YRecordingComponent(List<float> record) => _record = record;
        public override void OnDraw(ISpriteBatch spriteBatch) => _record.Add(Owner!.Position.Y);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject MakeGo(string name, float y, List<float> record)
    {
        var go = new GameObject(Guid.NewGuid(), name) { Position = new Vector2(0, y) };
        go.AddComponent(new YRecordingComponent(record));
        return go;
    }

    private static readonly StubSpriteBatch Batch = new();

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Draw_ThreeObjects_DrawnInAscendingYOrder()
    {
        var record = new List<float>();
        var scene  = new Scene([]);
        // Add in intentionally wrong order to confirm sorting is applied.
        scene.AddGameObject(MakeGo("Far",  10f, record));
        scene.AddGameObject(MakeGo("Near",  1f, record));
        scene.AddGameObject(MakeGo("Mid",   5f, record));

        scene.Draw(Batch);

        record.Should().Equal([1f, 5f, 10f],
            "lower Y (further back) must be drawn before higher Y (closer to camera)");
    }

    [Fact]
    public void Draw_ObjectsAlreadyInOrder_DrawnCorrectly()
    {
        var record = new List<float>();
        var scene  = new Scene([]);
        scene.AddGameObject(MakeGo("A", 0f,  record));
        scene.AddGameObject(MakeGo("B", 5f,  record));
        scene.AddGameObject(MakeGo("C", 10f, record));

        scene.Draw(Batch);

        record.Should().Equal([0f, 5f, 10f]);
    }

    [Fact]
    public void Draw_TieInY_BothDrawn()
    {
        var record = new List<float>();
        var scene  = new Scene([]);
        scene.AddGameObject(MakeGo("A", 3f, record));
        scene.AddGameObject(MakeGo("B", 3f, record));

        scene.Draw(Batch);

        // Both must draw; order between ties is unspecified.
        record.Should().HaveCount(2);
        record.Should().AllSatisfy(y => y.Should().Be(3f));
    }

    [Fact]
    public void Draw_SingleObject_Draws()
    {
        var record = new List<float>();
        var scene  = new Scene([]);
        scene.AddGameObject(MakeGo("Solo", 7f, record));

        scene.Draw(Batch);

        record.Should().Equal([7f]);
    }

    [Fact]
    public void Draw_EmptyScene_NoCalls()
    {
        var record = new List<float>();
        var scene  = new Scene([]);

        scene.Draw(Batch);

        record.Should().BeEmpty("no GameObjects → no draw calls");
    }

    [Fact]
    public void Draw_DoesNotAffectUpdateOrder()
    {
        // Update order is insertion order; draw order is Y-sorted.
        var updateOrder = new List<string>();
        var drawOrder   = new List<string>();

        var scene = new Scene([]);

        foreach (var (name, y) in new[] { ("Z", 10f), ("A", 1f), ("M", 5f) })
        {
            var go = new GameObject(Guid.NewGuid(), name) { Position = new Vector2(0, y) };
            go.AddComponent(new TrackBothComponent(name, updateOrder, drawOrder));
            scene.AddGameObject(go);
        }

        scene.Update(0.016f);
        scene.Draw(Batch);

        updateOrder.Should().Equal(["Z", "A", "M"],
            "Update fires in insertion order");
        drawOrder.Should().Equal(["A", "M", "Z"],
            "Draw fires in ascending Y order");
    }

    private sealed class TrackBothComponent : Component
    {
        private readonly string       _name;
        private readonly List<string> _updateLog;
        private readonly List<string> _drawLog;

        public TrackBothComponent(string name, List<string> updateLog, List<string> drawLog)
        {
            _name      = name;
            _updateLog = updateLog;
            _drawLog   = drawLog;
        }

        public override void OnUpdate(float deltaSeconds) => _updateLog.Add(_name);
        public override void OnDraw(ISpriteBatch spriteBatch) => _drawLog.Add(_name);
    }
}
