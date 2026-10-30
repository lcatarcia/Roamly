import { t } from '@/i18n/catalog'

type BrandLogoVariant = 'wordmark' | 'icon'

const LOGO_ASSETS: Record<BrandLogoVariant, { light: string; dark: string }> = {
  wordmark: {
    light: '/brand/logo-horizontal-light.svg',
    dark: '/brand/logo-horizontal-dark.svg',
  },
  icon: {
    light: '/brand/app-icon-light.svg',
    dark: '/brand/app-icon-dark.svg',
  },
}

export function BrandLogo({
  variant = 'wordmark',
  className = '',
}: {
  variant?: BrandLogoVariant
  className?: string
}) {
  const assets = LOGO_ASSETS[variant]

  return (
    <span className={`inline-flex shrink-0 ${className}`}>
      <img
        src={assets.light}
        alt={t('neutral.app.brand')}
        className="block h-full w-full object-contain dark:hidden"
      />
      <img
        src={assets.dark}
        alt={t('neutral.app.brand')}
        className="hidden h-full w-full object-contain dark:block"
      />
    </span>
  )
}
