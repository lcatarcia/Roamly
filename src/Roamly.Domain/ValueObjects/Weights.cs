namespace Roamly.Domain.ValueObjects;

/// <summary>Masse del mezzo. Unita' canonica: kg interi (CONTEXT.md §2.1).</summary>
/// <param name="KerbWeightKg">Massa in ordine di marcia.</param>
/// <param name="MaxWeightKg">Massa massima tecnicamente ammessa.</param>
public readonly record struct Weights(int KerbWeightKg, int MaxWeightKg);
