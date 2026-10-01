/**
 * Catalogo italiano unico. Le chiavi descrivono il registro: neutral per
 * messaggi funzionali e delicati, narrative per la microcopy di prodotto.
 */
export const catalog = {
  'neutral.app.brand': "Roamly",
  'neutral.theme.enableDark': "Attiva tema scuro",
  'neutral.theme.enableLight': "Attiva tema chiaro",
  'neutral.toast.close': "Chiudi notifica",
  'neutral.login.checkingSession': "Verifica della sessione...",
  'neutral.login.signedInAs': "Accesso effettuato come",
  'neutral.login.signOut': "Esci",
  'neutral.login.signingOut': "Disconnessione...",
  'neutral.login.signOutError': "Non e' stato possibile disconnettersi. Riprova.",
  'neutral.login.title': "Accedi",
  'neutral.login.description': "Entra con la tua email e la tua password.",
  'neutral.login.email': "Email",
  'neutral.login.password': "Password",
  'neutral.login.submit': "Accedi",
  'neutral.login.submitting': "Accesso in corso...",
  'neutral.login.tooManyAttempts': "Troppi tentativi di accesso. Riprova piu' tardi.",
  'neutral.login.invalidCredentials': "Email o password non corretti.",
  'neutral.ribbon.lastService': "Ultimo tagliando",
  'neutral.ribbon.today': "Adesso",
  'neutral.ribbon.nextService': "Prossimo tagliando",
  'neutral.ribbon.ariaLabel': "Esempio statico del Ribbon di manutenzione",
  'neutral.ribbon.remaining': "Mancano",
  'neutral.ribbon.prototypeLabel': "Prototipo dimostrativo",
  'neutral.ribbon.prototypeTitle': "Ribbon manutenzione",
  'neutral.ribbon.prototypeNotice':
    "Prototipo statico: i valori mostrati sono dimostrativi e non provengono da un veicolo o da una misurazione.",
  'neutral.ribbon.orientation': "Orientamento",
  'neutral.ribbon.horizontal': "Orizzontale",
  'neutral.ribbon.vertical': "Verticale",
  'narrative.ribbon.prototypeIntro': "La manutenzione, a colpo d’occhio.",
} as const

export type TranslationKey = keyof typeof catalog

/** Restituisce una voce del catalogo italiano con chiave verificata da TypeScript. */
export function t<K extends TranslationKey>(key: K): (typeof catalog)[K] {
  return catalog[key]
}
