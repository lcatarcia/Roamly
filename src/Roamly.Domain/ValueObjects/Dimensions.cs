namespace Roamly.Domain.ValueObjects;

/// <summary>Ingombro del mezzo. Unita' canonica: millimetri interi (CONTEXT.md §2.1).</summary>
/// <param name="LengthMm">Lunghezza in millimetri.</param>
/// <param name="WidthMm">Larghezza in millimetri.</param>
/// <param name="HeightMm">Altezza in millimetri.</param>
public readonly record struct Dimensions(int LengthMm, int WidthMm, int HeightMm);
