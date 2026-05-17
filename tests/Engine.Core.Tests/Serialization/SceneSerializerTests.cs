using System.Text.Json;
using AwesomeAssertions;
using Engine.Core.Math;
using Engine.Core.Scene;
using Engine.Core.Serialization;

namespace Engine.Core.Tests.Serialization;

public class SceneSerializerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SceneData SaveAndLoad(SceneData scene)
    {
        using var stream = new MemoryStream();
        SceneSerializer.Save(scene, stream);
        stream.Position = 0;
        return SceneSerializer.Load(stream);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_EmptyScene_PreservesMetadata()
    {
        var scene = new SceneData { Name = "EmptyLevel" };

        var loaded = SaveAndLoad(scene);

        loaded.FormatVersion.Should().Be(1);
        loaded.Name.Should().Be("EmptyLevel");
        loaded.GameObjects.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_SceneWithComponents_PreservesAllFieldTypes()
    {
        // ComponentData.Fields stores values as raw JsonElement.
        // A Vector2 in fields uses the "x,y" wire format produced by the converter.
        var scene = new SceneData
        {
            Name = "LevelOne",
            GameObjects =
            [
                new GameObjectData
                {
                    Name = "Hero",
                    Position = new Vector2(1.5f, 2.5f),
                    Components =
                    [
                        new ComponentData
                        {
                            TypeName = "Game.PlayerController",
                            Fields = new Dictionary<string, JsonElement>
                            {
                                ["health"]     = JsonSerializer.SerializeToElement(100),
                                ["label"]      = JsonSerializer.SerializeToElement("hero"),
                                ["speed"]      = JsonSerializer.SerializeToElement(5.5f),
                                ["spawnPoint"] = JsonSerializer.SerializeToElement("3,4"),
                            },
                        },
                    ],
                },
                new GameObjectData
                {
                    Name = "Enemy",
                    Position = new Vector2(10f, 20f),
                    Components =
                    [
                        new ComponentData
                        {
                            TypeName = "Game.EnemyAI",
                            Fields = new Dictionary<string, JsonElement>
                            {
                                ["damage"] = JsonSerializer.SerializeToElement(15),
                            },
                        },
                    ],
                },
            ],
        };

        var loaded = SaveAndLoad(scene);

        loaded.Name.Should().Be("LevelOne");
        loaded.GameObjects.Should().HaveCount(2);

        var hero = loaded.GameObjects[0];
        hero.Name.Should().Be("Hero");
        hero.Position.X.Should().BeApproximately(1.5f, 1e-5f);
        hero.Position.Y.Should().BeApproximately(2.5f, 1e-5f);
        hero.Components.Should().HaveCount(1);
        hero.Components[0].TypeName.Should().Be("Game.PlayerController");

        var fields = hero.Components[0].Fields;
        fields["health"].GetInt32().Should().Be(100);
        fields["label"].GetString().Should().Be("hero");
        fields["speed"].GetSingle().Should().BeApproximately(5.5f, 1e-4f);
        fields["spawnPoint"].GetString().Should().Be("3,4");

        var enemy = loaded.GameObjects[1];
        enemy.Name.Should().Be("Enemy");
        enemy.Position.X.Should().BeApproximately(10f, 1e-5f);
        enemy.Position.Y.Should().BeApproximately(20f, 1e-5f);
        enemy.Components[0].TypeName.Should().Be("Game.EnemyAI");
        enemy.Components[0].Fields["damage"].GetInt32().Should().Be(15);
    }

    [Fact]
    public void Load_FormatVersionNewerThanEngine_ThrowsSceneFormatException()
    {
        var scene = new SceneData { FormatVersion = 999 };
        using var stream = new MemoryStream();
        SceneSerializer.Save(scene, stream);
        stream.Position = 0;

        Action act = () => SceneSerializer.Load(stream);

        act.Should().Throw<SceneFormatException>()
            .WithMessage("*999*");
    }
}
