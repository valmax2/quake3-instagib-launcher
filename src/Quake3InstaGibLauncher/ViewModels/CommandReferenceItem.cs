namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>Una riga dell'elenco comandi/frasi copiabili (finestra "Elenco comandi"): descrizione
/// leggibile e testo esatto da incollare nel campo "Comando" quando si crea un tasto
/// personalizzato nella sezione Tasti.</summary>
public sealed record CommandReferenceItem(string Description, string Command);
