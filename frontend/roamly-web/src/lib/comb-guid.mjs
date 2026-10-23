const DOTNET_UNIX_EPOCH_TICKS = 621355968000000000n
const TICKS_PER_MILLISECOND = 10_000n

let lastCounter = BigInt(Date.now()) * TICKS_PER_MILLISECOND + DOTNET_UNIX_EPOCH_TICKS

/** Genera il Guid COMB ordinato per SQL Server, allineato al generatore .NET di Roamly. */
export function createCombGuid() {
  lastCounter += 1n
  return formatCombGuid(lastCounter, crypto.randomUUID())
}

/** Applica in modo deterministico il layout di byte del SequentialGuidGenerator .NET. */
export function formatCombGuid(counter, randomGuid) {
  const canonicalBytes = parseGuid(randomGuid)
  const guidBytes = Uint8Array.from([
    canonicalBytes[3], canonicalBytes[2], canonicalBytes[1], canonicalBytes[0],
    canonicalBytes[5], canonicalBytes[4],
    canonicalBytes[7], canonicalBytes[6],
    ...canonicalBytes.slice(8),
  ])

  const counterBytes = new Uint8Array(8)
  new DataView(counterBytes.buffer).setBigInt64(0, counter, true)
  guidBytes[8] = counterBytes[1]
  guidBytes[9] = counterBytes[0]
  guidBytes[10] = counterBytes[7]
  guidBytes[11] = counterBytes[6]
  guidBytes[12] = counterBytes[5]
  guidBytes[13] = counterBytes[4]
  guidBytes[14] = counterBytes[3]
  guidBytes[15] = counterBytes[2]

  const resultBytes = [
    ...guidBytes.slice(0, 4).reverse(),
    ...guidBytes.slice(4, 6).reverse(),
    ...guidBytes.slice(6, 8).reverse(),
    ...guidBytes.slice(8),
  ]
  const hex = resultBytes.map((value) => value.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

function parseGuid(value) {
  const hex = value.replaceAll('-', '')
  if (!/^[0-9a-f]{32}$/i.test(hex)) {
    throw new TypeError('A valid Guid is required.')
  }

  return Uint8Array.from({ length: 16 }, (_, index) =>
    Number.parseInt(hex.slice(index * 2, index * 2 + 2), 16),
  )
}
