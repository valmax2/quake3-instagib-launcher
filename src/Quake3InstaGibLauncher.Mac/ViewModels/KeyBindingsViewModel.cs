using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Mac.Models;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

/// <summary>
/// Schermata "Tasti": elenco di comandi Quake III gia' preimpostati ciascuno associabile a un
/// tasto a piacere, piu' la possibilita' di aggiungere comandi personalizzati. Applicati al lancio
/// del gioco tramite un file .cfg di "bind" generato al volo (vedi KeyBindingCfgService).
/// </summary>
public partial class KeyBindingsViewModel : ObservableObject
{
    private readonly MacAppSettings _settings;
    private readonly Action _saveSettings;

    public ObservableCollection<KeyBindingViewModel> Bindings { get; } = new();

    [ObservableProperty] private string _newCommand = string.Empty;
    [ObservableProperty] private string _newKey = string.Empty;
    [ObservableProperty] private string _newDescription = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public KeyBindingsViewModel(MacAppSettings settings, Action saveSettings)
    {
        _settings = settings;
        _saveSettings = saveSettings;

        if (_settings.KeyBindings is null || _settings.KeyBindings.Count == 0)
            _settings.KeyBindings = BuildDefaultPresets();

        foreach (var entry in _settings.KeyBindings)
            Bindings.Add(new KeyBindingViewModel(entry, Persist));
    }

    public IReadOnlyList<KeyBindingEntry> ActiveBindings => Bindings.Select(b => b.Model).ToList();

    [RelayCommand]
    private void AddCustomBinding()
    {
        var key = NewKey.Trim();
        var command = NewCommand.Trim();
        var description = string.IsNullOrWhiteSpace(NewDescription) ? command : NewDescription.Trim();

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(command))
        {
            StatusMessage = "Indica sia il tasto sia il comando.";
            return;
        }

        var entry = new KeyBindingEntry { Description = description, Command = command, Key = key, Enabled = true, IsBuiltIn = false };
        _settings.KeyBindings!.Add(entry);
        Bindings.Add(new KeyBindingViewModel(entry, Persist));

        NewCommand = string.Empty;
        NewKey = string.Empty;
        NewDescription = string.Empty;
        Persist();
        StatusMessage = $"Comando \"{command}\" aggiunto sul tasto {key.ToUpperInvariant()}.";
    }

    [RelayCommand]
    private void RemoveBinding(KeyBindingViewModel? binding)
    {
        if (binding is null) return;
        _settings.KeyBindings!.Remove(binding.Model);
        Bindings.Remove(binding);
        Persist();
        StatusMessage = $"\"{binding.Description}\" rimosso.";
    }

    [RelayCommand]
    private void ResetPresets()
    {
        _settings.KeyBindings = BuildDefaultPresets();
        Bindings.Clear();
        foreach (var entry in _settings.KeyBindings)
            Bindings.Add(new KeyBindingViewModel(entry, Persist));
        Persist();
        StatusMessage = "Tasti predefiniti ripristinati.";
    }

    private void Persist()
    {
        _settings.KeyBindings = Bindings.Select(b => b.Model).ToList();
        _saveSettings();
    }

    private static List<KeyBindingEntry> BuildDefaultPresets() => new()
    {
        new KeyBindingEntry { Description = "Salto", Command = "+moveup", Key = "SPACE", Enabled = true, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Accovacciati", Command = "+movedown", Key = "C", Enabled = true, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Corri (tieni premuto)", Command = "+speed", Key = "SHIFT", Enabled = true, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Usa/Azione", Command = "+button2", Key = "E", Enabled = false, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Tabella punteggi", Command = "+scores", Key = "TAB", Enabled = true, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Chat con tutti", Command = "messagemode", Key = "T", Enabled = true, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Chat con la squadra", Command = "messagemode2", Key = "Y", Enabled = false, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Cattura schermata", Command = "screenshot", Key = "F12", Enabled = true, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Arma: Gauntlet (mischia)", Command = "weapon 1", Key = "1", Enabled = false, IsBuiltIn = true },
        new KeyBindingEntry { Description = "Arma: Railgun/InstaGib", Command = "weapon 2", Key = "2", Enabled = false, IsBuiltIn = true },
    };
}
