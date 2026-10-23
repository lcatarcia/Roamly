export const TRIPS_QUERY_KEY = ['trips'] as const
export const TRIP_DETAILS_QUERY_KEY = ['trip-details'] as const
export const JOURNAL_ENTRIES_QUERY_KEY = ['journal-entries'] as const
export const TRIP_EXPENSES_QUERY_KEY = ['trip-expenses'] as const
export const TRIP_CHECKLISTS_QUERY_KEY = ['trip-checklists'] as const

export function tripDetailsQueryKey(tripId: string) {
  return [...TRIP_DETAILS_QUERY_KEY, tripId] as const
}

export function journalEntriesQueryKey(tripId: string) {
  return [...JOURNAL_ENTRIES_QUERY_KEY, tripId] as const
}

export function tripChecklistsQueryKey(tripId: string) {
  return [...TRIP_CHECKLISTS_QUERY_KEY, tripId] as const
}

export function tripExpensesQueryKey(camperId: string, tripId: string) {
  return [...TRIP_EXPENSES_QUERY_KEY, camperId, tripId] as const
}
