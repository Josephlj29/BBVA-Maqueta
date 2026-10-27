/**
 * The sandbox contracts BBVA associated with these credentials, taken from the official
 * test plan workbook.
 *
 * Typing an 18-digit account number by hand is a transposition waiting to happen, and a
 * wrong digit produces a 403 that looks identical to a permissions problem. Offering the
 * known-good set as a picker removes a whole class of false alarms during integration.
 */

export type SandboxProductType = 'ahorro' | 'corriente' | 'plazo';

export interface SandboxContract {
  readonly number: string;
  /** Short label for the picker. */
  readonly label: string;
  readonly product: SandboxProductType;
  readonly currency: 'PEN' | 'USD';
  /** Set when the contract is expected to behave unusually. */
  readonly note?: string;
  /** True when BBVA has deliberately not associated this contract with the API. */
  readonly accessDenied?: boolean;
  /** True when the contract has no movements at all. */
  readonly empty?: boolean;
}

export const SANDBOX_CONTRACTS: readonly SandboxContract[] = [
  {
    number: '001101300210484377',
    label: 'Ahorro soles',
    product: 'ahorro',
    currency: 'PEN',
    note: 'Movimientos de intereses ganados (INCOME)',
  },
  {
    number: '001101300105089306',
    label: 'Corriente soles',
    product: 'corriente',
    currency: 'PEN',
    note: 'Varias páginas — sirve para probar paginación',
  },
  {
    number: '001101300105109854',
    label: 'Corriente dólares',
    product: 'corriente',
    currency: 'USD',
    note: 'Responde a todos los filtros combinados',
  },
  {
    number: '001101300105114734',
    label: 'Corriente soles (pagos)',
    product: 'corriente',
    currency: 'PEN',
  },
  {
    number: '001101300210515493',
    label: 'Ahorro dólares',
    product: 'ahorro',
    currency: 'USD',
  },
  {
    number: '001101300301020152',
    label: 'Plazo soles',
    product: 'plazo',
    currency: 'PEN',
  },
  {
    number: '001101300301019189',
    label: 'Plazo dólares',
    product: 'plazo',
    currency: 'USD',
  },
  {
    number: '001101300105109846',
    label: 'Corriente soles (bloqueada)',
    product: 'corriente',
    currency: 'PEN',
    note: 'Cuenta bloqueada — igual devuelve movimientos',
  },
  {
    number: '001101300105109862',
    label: 'Sin movimientos',
    product: 'ahorro',
    currency: 'PEN',
    note: 'Devuelve 200 con "No transactions to show."',
    empty: true,
  },
  {
    number: '001101300105109811',
    label: 'Sin coincidencias por fecha',
    product: 'ahorro',
    currency: 'PEN',
    note: 'Vacío al filtrar por noviembre 2024',
  },
  {
    number: '001101300210522600',
    label: 'No asociada a la API',
    product: 'corriente',
    currency: 'PEN',
    note: 'Devuelve 403 Access denied a propósito',
    accessDenied: true,
  },
];

/** A movement id known to exist, for seeding the detail lookup. */
export const SANDBOX_SAMPLE_MOVEMENT = {
  accountId: '001101300105109846',
  transactionId: 'PE00110201300105109846000000014',
} as const;
