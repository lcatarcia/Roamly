import { useState } from 'react'

import { PageContainer } from '@/components/layout/PageContainer'
import { Button } from '@/components/ui/button'
import { t } from '@/i18n/catalog'

import { Ribbon, type RibbonOrientation } from './Ribbon'

export function RibbonPrototypePage() {
  const [orientation, setOrientation] = useState<RibbonOrientation>('horizontal')

  return (
    <PageContainer className="max-w-3xl justify-start gap-6">
      <header className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">
          {t('neutral.ribbon.prototypeLabel')}
        </p>
        <h1 className="page-title">{t('neutral.ribbon.prototypeTitle')}</h1>
        <p className="text-sm text-muted-foreground">
          {t('narrative.ribbon.prototypeIntro')}
        </p>
      </header>

      <p role="note" className="rounded-md border border-border-strong bg-muted p-4 text-sm">
        {t('neutral.ribbon.prototypeNotice')}
      </p>

      <fieldset className="space-y-3">
        <legend className="text-sm font-medium">{t('neutral.ribbon.orientation')}</legend>
        <div className="flex flex-wrap gap-3">
          <Button
            type="button"
            className="min-h-11"
            variant={orientation === 'horizontal' ? 'secondary' : 'outline'}
            aria-pressed={orientation === 'horizontal'}
            onClick={() => setOrientation('horizontal')}
          >
            {t('neutral.ribbon.horizontal')}
          </Button>
          <Button
            type="button"
            className="min-h-11"
            variant={orientation === 'vertical' ? 'secondary' : 'outline'}
            aria-pressed={orientation === 'vertical'}
            onClick={() => setOrientation('vertical')}
          >
            {t('neutral.ribbon.vertical')}
          </Button>
        </div>
      </fieldset>

      <Ribbon orientation={orientation} />
    </PageContainer>
  )
}
