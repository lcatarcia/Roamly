/** Genera un Guid COMB ordinato per SQL Server usando l'istante UTC corrente. */
export function createCombGuid(): string

/** Applica il layout di byte del SequentialGuidGenerator .NET a input fissati. */
export function formatCombGuid(counter: bigint, randomGuid: string): string
