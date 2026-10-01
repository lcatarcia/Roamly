import { t } from '@/i18n/catalog'

export type RibbonOrientation = 'horizontal' | 'vertical'

interface RibbonProps {
  orientation: RibbonOrientation
}

const kilometerFormat = new Intl.NumberFormat('it-IT', {
  style: 'unit',
  unit: 'kilometer',
  unitDisplay: 'short',
  maximumFractionDigits: 0,
})

const dateFormat = new Intl.DateTimeFormat('it-IT', {
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
})

const demonstrationPoints = [
  {
    key: 'last',
    label: t('neutral.ribbon.lastService'),
    value: kilometerFormat.format(82400),
    detail: dateFormat.format(new Date(Date.UTC(2026, 2, 1))),
    current: false,
  },
  {
    key: 'today',
    label: t('neutral.ribbon.today'),
    value: kilometerFormat.format(94120),
    detail: '',
    current: true,
  },
  {
    key: 'next',
    label: t('neutral.ribbon.nextService'),
    value: kilometerFormat.format(97400),
    detail: dateFormat.format(new Date(Date.UTC(2026, 8, 1))),
    current: false,
  },
] as const

const remainingDistance = kilometerFormat.format(3280)

/**
 * Prototipo visual senza dati di dominio. I punti e la loro spaziatura sono
 * dimostrativi: la linea non rappresenta una scala ne' una misura calcolata.
 */
export function Ribbon({ orientation }: RibbonProps) {
  const isHorizontal = orientation === 'horizontal'

  return (
    <section
      aria-label={t('neutral.ribbon.ariaLabel')}
      className="rounded-xl border border-border-strong bg-secondary p-5 sm:p-7"
    >
      <div className="relative">
        <span
          aria-hidden="true"
          className={
            isHorizontal
              ? 'absolute inset-x-[16.666%] top-8 h-px bg-border-strong'
              : 'absolute bottom-2 left-[7px] top-2 w-px bg-border-strong'
          }
        />
        <ol className={isHorizontal ? 'grid grid-cols-3 gap-2 pt-6' : 'flex flex-col gap-7'}>
          {demonstrationPoints.map((point) => (
            <li
              key={point.key}
              className={
                isHorizontal
                  ? 'relative flex flex-col items-center pt-1 text-center'
                  : 'relative pl-7'
              }
            >
              <span
                aria-hidden="true"
                className={
                  isHorizontal
                    ? point.current
                      ? 'absolute left-1/2 top-0 size-4 -translate-x-1/2 rotate-45 rounded-sm bg-primary'
                      : 'absolute left-1/2 top-0 size-4 -translate-x-1/2 rounded-full border-2 border-border-strong bg-background'
                    : point.current
                      ? 'absolute left-0 top-1 size-4 rotate-45 rounded-sm bg-primary'
                      : 'absolute left-0 top-1 size-4 rounded-full border-2 border-border-strong bg-background'
                }
              />
              <p className="text-xs font-medium text-muted-foreground sm:text-sm">
                {point.label}
              </p>
              <p className="tabular-figures mt-1 text-sm font-semibold text-foreground sm:text-base">
                {point.value}
              </p>
              {point.detail && (
                <p className="mt-1 text-xs text-muted-foreground">{point.detail}</p>
              )}
            </li>
          ))}
        </ol>
      </div>
      <p className="mt-6 flex items-baseline gap-2 border-t border-border pt-4 text-sm text-muted-foreground">
        <span>{t('neutral.ribbon.remaining')}</span>
        <strong className="tabular-figures font-semibold text-foreground">
          {remainingDistance}
        </strong>
      </p>
    </section>
  )
}
