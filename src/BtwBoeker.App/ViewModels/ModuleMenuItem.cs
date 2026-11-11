using System.Windows.Input;

namespace BtwBoeker.App.ViewModels;

/// <summary>Eén item in het menu Modules.</summary>
public sealed record ModuleMenuItem(string Titel, string? Pictogram, string? Sneltoets, ICommand Opdracht, object Parameter);
