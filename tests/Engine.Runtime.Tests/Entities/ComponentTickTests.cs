using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Runtime.Entities;

namespace Engine.Runtime.Tests.Entities;

/// <summary>Tests that Components tick with correct delta and that multiple components all fire.</summary>
public class ComponentTickTests
{
    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Records each OnUpdate invocation for assertion.</summary>
    private sealed class CountingComponent : Component
    {
        public int   CallCount  { get; private set; }
        public float LastDelta  { get; private set; }

        public override void OnUpdate(float deltaSeconds)
        {
            CallCount++;
            LastDelta = deltaSeconds;
        }
    }

    private static Scene MakeScene() => new Scene([]);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Component_OnUpdate_CalledOncePerSceneUpdate_WithCorrectDelta()
    {
        var comp = new CountingComponent();
        var go   = new GameObject(Guid.NewGuid(), "Actor");
        go.AddComponent(comp);

        var scene = MakeScene();
        scene.AddGameObject(go);
        scene.Update(0.016f);

        comp.CallCount.Should().Be(1, "one Update call → one OnUpdate call");
        comp.LastDelta.Should().BeApproximately(0.016f, 1e-6f, "delta must pass through unchanged");
    }

    [Fact]
    public void Component_OnUpdate_NotCalledOnGameObjectNotInScene()
    {
        var comp = new CountingComponent();
        var go   = new GameObject(Guid.NewGuid(), "Orphan");
        go.AddComponent(comp);

        // Scene has no GameObjects — Update should not reach the component.
        var scene = MakeScene();
        scene.Update(0.016f);

        comp.CallCount.Should().Be(0, "GameObject was never added to the scene");
    }

    [Fact]
    public void Component_OnUpdate_CalledMultipleTimes_AccumulatesDelta()
    {
        var comp = new CountingComponent();
        var go   = new GameObject(Guid.NewGuid(), "Actor");
        go.AddComponent(comp);

        var scene = MakeScene();
        scene.AddGameObject(go);

        scene.Update(0.016f);
        scene.Update(0.033f);
        scene.Update(0.016f);

        comp.CallCount.Should().Be(3, "one OnUpdate per Scene.Update call");
        comp.LastDelta.Should().BeApproximately(0.016f, 1e-6f, "last delta passed through correctly");
    }

    [Fact]
    public void MultipleComponents_OnSameGameObject_AllTick()
    {
        var a = new CountingComponent();
        var b = new CountingComponent();
        var c = new CountingComponent();

        var go = new GameObject(Guid.NewGuid(), "Multi");
        go.AddComponent(a);
        go.AddComponent(b);
        go.AddComponent(c);

        var scene = MakeScene();
        scene.AddGameObject(go);
        scene.Update(0.1f);

        a.CallCount.Should().Be(1, "component A must tick");
        b.CallCount.Should().Be(1, "component B must tick");
        c.CallCount.Should().Be(1, "component C must tick");
        a.LastDelta.Should().BeApproximately(0.1f, 1e-6f);
        b.LastDelta.Should().BeApproximately(0.1f, 1e-6f);
        c.LastDelta.Should().BeApproximately(0.1f, 1e-6f);
    }

    [Fact]
    public void MultipleGameObjects_AllTick_Independently()
    {
        var compA = new CountingComponent();
        var compB = new CountingComponent();

        var goA = new GameObject(Guid.NewGuid(), "A") { Position = new Vector2(0, 0) };
        var goB = new GameObject(Guid.NewGuid(), "B") { Position = new Vector2(10, 10) };
        goA.AddComponent(compA);
        goB.AddComponent(compB);

        var scene = MakeScene();
        scene.AddGameObject(goA);
        scene.AddGameObject(goB);
        scene.Update(0.05f);

        compA.CallCount.Should().Be(1);
        compB.CallCount.Should().Be(1);
    }

    [Fact]
    public void Component_Owner_IsSetAfterAddComponent()
    {
        var comp = new CountingComponent();
        var go   = new GameObject(Guid.NewGuid(), "Owner");
        go.AddComponent(comp);

        comp.Owner.Should().BeSameAs(go, "Owner must point to the containing GameObject");
    }

    [Fact]
    public void Component_Owner_IsNullAfterRemoveComponent()
    {
        var comp = new CountingComponent();
        var go   = new GameObject(Guid.NewGuid(), "Owner");
        go.AddComponent(comp);
        go.RemoveComponent(comp);

        comp.Owner.Should().BeNull("Owner must be cleared on detach");
    }
}
