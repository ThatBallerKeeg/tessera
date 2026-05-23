using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Scene;
using Engine.Core.Tiles;
using Engine.Runtime.Components;
using Engine.Runtime.Entities;

namespace Engine.Runtime.Tests.Entities;

/// <summary>Tests that SceneLoader correctly instantiates a live Scene from SceneData.</summary>
public class SceneLoaderTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SceneData MakeScene(params GameObjectData[] objects)
    {
        var s = new SceneData { Name = "Test" };
        foreach (var o in objects) s.GameObjects.Add(o);
        return s;
    }

    private static GameObjectData MakeGod(
        string name,
        Vector2 position,
        params string[] componentTypeNames)
    {
        var god = new GameObjectData { Name = name, Position = position };
        foreach (var tn in componentTypeNames)
            god.Components.Add(new ComponentData { TypeName = tn });
        return god;
    }

    // ── Basic scene shape ─────────────────────────────────────────────────────

    [Fact]
    public void Load_EmptyScene_ReturnsSceneWithNoGameObjects()
    {
        var scene = SceneLoader.Load(MakeScene());
        scene.GameObjects.Should().BeEmpty();
    }

    [Fact]
    public void Load_SceneWithTilemaps_LayersExposedinScene()
    {
        var data = new SceneData { Name = "WithTilemaps" };
        data.Tilemaps.Add(new TilemapData { LayerName = "floor",  LayerIndex = 0 });
        data.Tilemaps.Add(new TilemapData { LayerName = "decor",  LayerIndex = 1 });

        var scene = SceneLoader.Load(data);

        scene.TilemapLayers.Should().HaveCount(2);
        scene.TilemapLayers[0].LayerName.Should().Be("floor");
        scene.TilemapLayers[1].LayerName.Should().Be("decor");
    }

    [Fact]
    public void Load_TilemapLayers_SortedByLayerIndex()
    {
        var data = new SceneData();
        data.Tilemaps.Add(new TilemapData { LayerName = "walls", LayerIndex = 2 });
        data.Tilemaps.Add(new TilemapData { LayerName = "floor", LayerIndex = 0 });
        data.Tilemaps.Add(new TilemapData { LayerName = "decor", LayerIndex = 1 });

        var scene = SceneLoader.Load(data);

        scene.TilemapLayers.Select(l => l.LayerName)
             .Should().Equal(["floor", "decor", "walls"]);
    }

    // ── GameObject identity ───────────────────────────────────────────────────

    [Fact]
    public void Load_GameObjectData_PreservesNameAndPosition()
    {
        var pos  = new Vector2(12f, 34f);
        var data = MakeScene(MakeGod("Hero", pos));

        var scene = SceneLoader.Load(data);

        scene.GameObjects.Should().HaveCount(1);
        var go = scene.GameObjects[0];
        go.Name.Should().Be("Hero");
        go.Position.Should().Be(pos);
    }

    [Fact]
    public void Load_GameObjectData_PreservesGuid()
    {
        var id   = Guid.NewGuid();
        var god  = new GameObjectData { Id = id, Name = "Tracked", Position = Vector2.Zero };
        var data = MakeScene(god);

        var scene = SceneLoader.Load(data);

        scene.GameObjects[0].Id.Should().Be(id);
    }

    [Fact]
    public void Load_MultipleGameObjects_AllPresent()
    {
        var data = MakeScene(
            MakeGod("A", new Vector2(0, 0)),
            MakeGod("B", new Vector2(1, 1)),
            MakeGod("C", new Vector2(2, 2)));

        var scene = SceneLoader.Load(data);

        scene.GameObjects.Select(g => g.Name)
             .Should().Equal(["A", "B", "C"]);
    }

    // ── Engine-builtin component round-trip ───────────────────────────────────

    [Fact]
    public void Load_SpriteRendererComponentData_InstantiatesSpriteRenderer()
    {
        var data = MakeScene(MakeGod("Player", Vector2.Zero, "SpriteRenderer"));

        var scene = SceneLoader.Load(data);

        var go = scene.GameObjects[0];
        go.Components.Should().HaveCount(1);
        go.Components[0].Should().BeOfType<SpriteRenderer>();
    }

    [Fact]
    public void Load_AnimatorComponentData_InstantiatesAnimator()
    {
        var data = MakeScene(MakeGod("Player", Vector2.Zero, "Animator"));

        var scene = SceneLoader.Load(data);

        scene.GameObjects[0].GetComponent<Animator>()
             .Should().NotBeNull("Animator component must be instantiated from TypeName");
    }

    [Fact]
    public void Load_SpriteRendererAndAnimator_BothAttached()
    {
        var data = MakeScene(
            MakeGod("Player", Vector2.Zero, "SpriteRenderer", "Animator"));

        var scene = SceneLoader.Load(data);

        var go = scene.GameObjects[0];
        go.Components.Should().HaveCount(2);
        go.GetComponent<SpriteRenderer>().Should().NotBeNull();
        go.GetComponent<Animator>().Should().NotBeNull();
    }

    [Fact]
    public void Load_UnknownComponentTypeName_SkippedWithoutException()
    {
        // SceneLoader must tolerate unknown types (Phase 3 user-scripted components).
        var data = MakeScene(
            MakeGod("Actor", Vector2.Zero, "UserMonster", "SpriteRenderer"));

        Scene scene = null!;
        var act = () => { scene = SceneLoader.Load(data); };
        act.Should().NotThrow("unknown component types must be skipped, not thrown");

        // Only the known SpriteRenderer should survive.
        scene.GameObjects[0].Components.Should().HaveCount(1);
        scene.GameObjects[0].Components[0].Should().BeOfType<SpriteRenderer>();
    }

    [Fact]
    public void Load_Component_OwnerIsSetAfterLoad()
    {
        var data = MakeScene(MakeGod("Hero", Vector2.Zero, "SpriteRenderer"));

        var scene = SceneLoader.Load(data);

        var sr = scene.GameObjects[0].GetComponent<SpriteRenderer>()!;
        sr.Owner.Should().BeSameAs(scene.GameObjects[0],
            "SceneLoader must call AddComponent which sets Owner");
    }
}
