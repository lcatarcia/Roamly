import { t } from '@/i18n/catalog'

type BrandLogoVariant = 'wordmark' | 'icon'

const LOGO_ASSETS: Record<BrandLogoVariant, { light: string; dark: string; width: number; height: number }> = {
  wordmark: {
    light: '/brand/logo-horizontal-light.png',
    dark: '/brand/logo-horizontal-dark.png',
    width: 1300,
    height: 332,
  },
  icon: {
    light: '/brand/app-icon.png',
    dark: '/brand/app-icon.png',
    width: 319,
    height: 319,
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
        width={assets.width}
        height={assets.height}
        alt={t('neutral.app.brand')}
        className="block h-full w-full object-contain dark:hidden"
      />
      <img
        src={assets.dark}
        width={assets.width}
        height={assets.height}
        alt={t('neutral.app.brand')}
        className="hidden h-full w-full object-contain dark:block"
      />
    </span>
  )
}
