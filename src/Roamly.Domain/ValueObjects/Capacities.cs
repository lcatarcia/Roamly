namespace Roamly.Domain.ValueObjects;

/// <summary>Capacita' di bordo. Unita' canoniche: litri e kg interi (CONTEXT.md §2.1).</summary>
/// <param name="FreshWaterL">Acqua pulita in litri.</param>
/// <param name="GreyWaterL">Acque grigie in litri.</param>
/// <param name="FuelTankL">Serbatoio carburante in litri.</param>
/// <param name="GasKg">Gas in kg.</param>
public readonly record struct Capacities(int FreshWaterL, int GreyWaterL, int FuelTankL, int GasKg);
