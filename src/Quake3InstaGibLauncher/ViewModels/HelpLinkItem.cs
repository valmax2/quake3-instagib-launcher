namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>Una voce della sezione "dove scaricare i componenti" nella Guida.</summary>
public sealed record HelpLinkItem(string Title, string Description, string Url);

/// <summary>Una voce della guida rapida all'installazione (passo numerato).</summary>
public sealed record HelpStepItem(int Number, string Title, string Description);
