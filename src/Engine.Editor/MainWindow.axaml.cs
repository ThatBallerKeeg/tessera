using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Engine.Core.Scene;
using Engine.Core.Serialization;

namespace Engine.Editor;

/// <summary>The main editor window: File menu, four-panel layout, and SceneSerializer integration.</summary>
public partial class MainWindow : Window
{
    private SceneData _scene = new();
    private string? _currentPath;

    public MainWindow()
    {
        InitializeComponent();

        SceneNameBox.Text = _scene.Name;
        SceneNameBox.TextChanged += OnSceneNameChanged;

        MenuNewScene.Click  += OnNewScene;
        MenuOpenScene.Click += OnOpenScene;
        MenuSaveScene.Click += OnSaveScene;
        MenuExit.Click      += (_, _) => Close();
    }

    private void OnSceneNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (SceneNameBox.Text is { } name)
            _scene.Name = name;
    }

    private void OnNewScene(object? sender, RoutedEventArgs e)
    {
        _scene = new SceneData();
        _currentPath = null;
        SceneNameBox.Text = _scene.Name;
        Title = "Tessera Engine Editor";
    }

    private async void OnOpenScene(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this)!;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Scene",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Tessera Scene") { Patterns = ["*.tscene"] },
                new FilePickerFileType("All Files")     { Patterns = ["*"] },
            ],
        });

        if (files is not [var file])
            return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            _scene = SceneSerializer.Load(stream);
            _currentPath = file.Path.LocalPath;
            SceneNameBox.Text = _scene.Name;
            Title = $"Tessera Engine Editor — {Path.GetFileName(_currentPath)}";
        }
        catch (SceneFormatException ex)
        {
            Console.Error.WriteLine($"[Editor] Failed to open scene: {ex.Message}");
        }
    }

    private async void OnSaveScene(object? sender, RoutedEventArgs e)
    {
        if (_currentPath is not null)
        {
            await using var fs = File.Create(_currentPath);
            SceneSerializer.Save(_scene, fs);
            return;
        }

        var topLevel = GetTopLevel(this)!;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Scene",
            DefaultExtension = "tscene",
            SuggestedFileName = _scene.Name,
            FileTypeChoices =
            [
                new FilePickerFileType("Tessera Scene") { Patterns = ["*.tscene"] },
            ],
        });

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        SceneSerializer.Save(_scene, stream);
        _currentPath = file.Path.LocalPath;
        Title = $"Tessera Engine Editor — {Path.GetFileName(_currentPath)}";
    }
}
