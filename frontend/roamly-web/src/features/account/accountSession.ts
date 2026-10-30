import { useEffect, useRef } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  ACCOUNT_DELETION_QUERY_KEY,
  clearAccountScopedQueries,
  ME_QUERY_KEY,
} from '@/features/account/accountQueries'
import { fetchMe, logout } from '@/lib/api'

/** Lettura condivisa della sessione: errori diversi dal 401 restano errori. */
export function useAccountSession() {
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: fetchMe,
    staleTime: Infinity,
    retry: false,
  })
  const lastMode = useRef(query.data?.mode)

  useEffect(() => {
    if (query.data?.mode === 'limited' && lastMode.current !== 'limited') {
      lastMode.current = 'limited'
      clearAccountScopedQueries(queryClient, { preserveDeletionStatus: true })
      queryClient.setQueryData(ME_QUERY_KEY, query.data)
      void queryClient.invalidateQueries({ queryKey: ACCOUNT_DELETION_QUERY_KEY })
      return
    }

    lastMode.current = query.data?.mode
  }, [query.data, queryClient])

  return query
}

/** Logout e purge account-scoped centralizzati per tutte le superfici UI. */
export function useAccountLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: logout,
    onSuccess: () => {
      clearAccountScopedQueries(queryClient)
      queryClient.setQueryData(ME_QUERY_KEY, null)
    },
  })
}
