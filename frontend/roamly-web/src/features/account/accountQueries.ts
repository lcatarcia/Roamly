import type { QueryClient } from '@tanstack/react-query'

import {
  CAMPER_DASHBOARD_QUERY_KEY,
  CAMPER_PROFILE_QUERY_KEY,
  CAMPERS_QUERY_KEY,
  EQUIPMENT_QUERY_KEY,
  MAINTENANCE_LOGS_QUERY_KEY,
} from '@/features/campers/queryKeys'
import {
  JOURNAL_ENTRIES_QUERY_KEY,
  TRIP_DETAILS_QUERY_KEY,
  TRIPS_QUERY_KEY,
} from '@/features/trips/queryKeys'

export const ME_QUERY_KEY = ['me'] as const
export const ACCOUNT_DELETION_QUERY_KEY = ['account', 'deletion'] as const
export const ACCOUNT_DELETION_PREVIEW_QUERY_KEY = ['account', 'deletion-preview'] as const

/** Pulisce i payload account-scoped e resetta la sessione senza scollegare gli observer. */
export function clearAccountScopedQueries(
  queryClient: QueryClient,
  options: { preserveDeletionStatus?: boolean } = {},
): void {
  const accountScopedKeys = [
    CAMPERS_QUERY_KEY,
    CAMPER_DASHBOARD_QUERY_KEY,
    CAMPER_PROFILE_QUERY_KEY,
    EQUIPMENT_QUERY_KEY,
    MAINTENANCE_LOGS_QUERY_KEY,
    TRIPS_QUERY_KEY,
    TRIP_DETAILS_QUERY_KEY,
    JOURNAL_ENTRIES_QUERY_KEY,
    ACCOUNT_DELETION_PREVIEW_QUERY_KEY,
  ]

  for (const queryKey of accountScopedKeys) {
    queryClient.removeQueries({ queryKey })
  }

  queryClient.setQueryData(ME_QUERY_KEY, null)

  if (!options.preserveDeletionStatus) {
    queryClient.removeQueries({ queryKey: ACCOUNT_DELETION_QUERY_KEY })
  }
}
