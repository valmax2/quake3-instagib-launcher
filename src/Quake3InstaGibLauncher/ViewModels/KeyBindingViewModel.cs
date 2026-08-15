using CommunityToolkit.Mvvm.ComponentModel;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>Wrapper bindabile attorno a un KeyBindingEntry: ogni modifica scrive subito nel
/// modello condiviso e salva subito (stesso schema di CustomizeViewModel/KnownPlayerViewModel).</summary>
public partial class KeyBindingViewModel : ObservableObject
{
    public KeyBindingEntry Model { get; }
    private readonly Action _persist;

    public string Description => Model.Description;
    public bool IsBuiltIn => Model.IsBuiltIn;

    [ObservableProperty] private string _command;
    [ObservableProperty] private string _key;
    [ObservableProperty] private bool _enabled;

    public KeyBindingViewModel(KeyBindingEntry model, Action persist)
    {
        Model = model;
        _persist = persist;
        _command = model.Command;
        _key = model.Key;
        _enabled = model.Enabled;
    }

    partial void OnCommandChanged(string value) { Model.Command = value; _persist(); }
    partial void OnKeyChanged(string value) { Model.Key = value; _persist(); } // normalizzato in maiuscolo solo al momento di scrivere il .cfg (KeyBindingCfgService)
    partial void OnEnabledChanged(bool value) { Model.Enabled = value; _persist(); }
}
