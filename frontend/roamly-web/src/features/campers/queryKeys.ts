export const CAMPERS_QUERY_KEY = ['campers'] as const
export const CAMPER_DASHBOARD_QUERY_KEY = ['camper-dashboard'] as const
export const CAMPER_PROFILE_QUERY_KEY = ['camper-profile'] as const
export const EQUIPMENT_QUERY_KEY = ['camper-equipment'] as const
export const MAINTENANCE_LOGS_QUERY_KEY = ['maintenance-logs'] as const

export function camperProfileQueryKey(camperId: string) {
  return [...CAMPER_PROFILE_QUERY_KEY, camperId] as const
}

export function equipmentQueryKey(camperId: string) {
  return [...EQUIPMENT_QUERY_KEY, camperId] as const
}

export function maintenanceLogsQueryKey(camperId: string, maintenanceItemId: string) {
  return [...MAINTENANCE_LOGS_QUERY_KEY, camperId, maintenanceItemId] as const
}
