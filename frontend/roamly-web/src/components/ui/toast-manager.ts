import { Toast as ToastPrimitive } from '@base-ui/react/toast'

/** API del manager separata dai componenti per mantenere pulito il boundary HMR. */
export const toast = ToastPrimitive.createToastManager()
export const createToastManager = ToastPrimitive.createToastManager
export const useToastManager = ToastPrimitive.useToastManager
